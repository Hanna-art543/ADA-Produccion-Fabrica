using System;
using System.Collections.Generic;
using Fabrica.Core;

namespace Fabrica.Escena
{
    /// <summary>
    /// Lógica de tiempo de la escena (Persona 3), SIN UnityEngine para poder probarla fuera de Unity.
    /// Todo es una función pura del tiempo t: se puede pausar, reiniciar o saltar sin desfases.
    ///
    /// Ventana visual de viaje (el Core no tiene tiempo de traslado):
    ///   T_v   = fraccionViaje       · makespan
    ///   T_min = fraccionViajeMinimo · makespan
    ///   salida hacia la etapa k: ws = max(fin[k-1], inicio[k] − T_v)
    ///   si inicio[k] − ws &lt; T_min  →  ws = inicio[k] − T_min   (pero sin salir antes de la mitad de la etapa anterior)
    ///   llegada a la máquina = inicio[k]  (coincide con el plan)
    /// Integración: el reloj va de 0 a makespan (igual que el Gantt). Tras la última etapa la pieza viaja
    /// a la salida y llega a más tardar en t = makespan, así que en t = makespan todas están en la salida.
    /// </summary>
    public class TiempoPiezas
    {
        public enum Tipo { Cola, Viaje, Procesando, Espera, ViajeSalida, Salida }

        public struct Estado
        {
            public Tipo tipo;
            public int etapa;          // índice de la etapa actual / destino (0..2)
            public int maquinaDesde;   // -1 = cola de entrada
            public int maquinaHasta;   // -1 = salida
            public float progreso;     // 0..1 en viajes y procesando
        }

        private class Pieza
        {
            public int id;
            public List<Assignment> etapas = new List<Assignment>();   // ordenadas por etapa
            public double[] salida;      // instante de salida hacia cada etapa
            public double llegadaSalida; // llegada a la cola de salida
        }

        private readonly List<Pieza> piezas = new List<Pieza>();
        private readonly Dictionary<int, List<Assignment>> porMaquina = new Dictionary<int, List<Assignment>>();
        private readonly List<Assignment> todas = new List<Assignment>();

        public double Makespan { get; private set; }
        public double Tv { get; private set; }
        public double Tmin { get; private set; }
        public int CantidadPiezas { get { return piezas.Count; } }
        public double EnergiaTotal { get; private set; }

        public TiempoPiezas(PlanResult plan, double fraccionViaje = 0.06, double fraccionViajeMinimo = 0.015)
        {
            var mapa = new Dictionary<int, Pieza>();
            double fmax = 0;
            if (plan != null && plan.asignaciones != null)
            {
                foreach (Assignment a in plan.asignaciones)
                {
                    Pieza p;
                    if (!mapa.TryGetValue(a.piezaId, out p)) { p = new Pieza { id = a.piezaId }; mapa[a.piezaId] = p; }
                    p.etapas.Add(a);
                    List<Assignment> l;
                    if (!porMaquina.TryGetValue(a.maquinaId, out l)) { l = new List<Assignment>(); porMaquina[a.maquinaId] = l; }
                    l.Add(a);
                    todas.Add(a);
                    EnergiaTotal += a.energia;
                    if (a.fin > fmax) fmax = a.fin;
                }
            }
            Makespan = Math.Max(plan != null ? plan.makespan : 0, fmax);
            Tv = fraccionViaje * Makespan;
            Tmin = Math.Min(fraccionViajeMinimo * Makespan, Tv);

            var ids = new List<int>(mapa.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                Pieza p = mapa[id];
                p.etapas.Sort((x, y) => ((int)x.etapa).CompareTo((int)y.etapa));
                int n = p.etapas.Count;
                p.salida = new double[n];
                for (int k = 0; k < n; k++)
                {
                    double ini = p.etapas[k].inicio;
                    double finPrev = k == 0 ? 0 : p.etapas[k - 1].fin;
                    double iniPrev = k == 0 ? 0 : p.etapas[k - 1].inicio;
                    double ws = Math.Max(finPrev, ini - Tv);
                    if (ini - ws < Tmin) ws = ini - Tmin;
                    // la pieza se ve procesando al menos la mitad de su etapa anterior (tareas más cortas que T_min)
                    if (k > 0 && ws < iniPrev + 0.5 * (finPrev - iniPrev)) ws = iniPrev + 0.5 * (finPrev - iniPrev);
                    if (ws < 0) ws = 0;
                    if (ws > ini) ws = ini;
                    p.salida[k] = ws;
                }
                double finUlt = n > 0 ? p.etapas[n - 1].fin : 0;
                p.llegadaSalida = Math.Min(finUlt + Tv, Makespan);
                if (p.llegadaSalida < finUlt) p.llegadaSalida = finUlt;
                piezas.Add(p);
            }
            foreach (List<Assignment> l in porMaquina.Values) l.Sort((x, y) => x.inicio.CompareTo(y.inicio));
        }

        public int IdPieza(int indice) { return piezas[indice].id; }

        /// <summary>Dónde está la pieza (por índice 0..CantidadPiezas-1) en el instante t.</summary>
        public Estado EstadoPieza(int indice, double t)
        {
            Pieza p = piezas[indice];
            var e = new Estado { maquinaDesde = -1, maquinaHasta = -1 };
            int n = p.etapas.Count;
            if (n == 0) { e.tipo = Tipo.Cola; return e; }

            for (int k = 0; k < n; k++)
            {
                Assignment a = p.etapas[k];
                double sal = p.salida[k];
                if (t < sal)
                {
                    if (k == 0) { e.tipo = Tipo.Cola; return e; }
                    // todavía en la máquina anterior: procesando o esperando
                    Assignment prev = p.etapas[k - 1];
                    e.etapa = k - 1;
                    e.maquinaDesde = prev.maquinaId;
                    e.maquinaHasta = prev.maquinaId;
                    if (t < prev.fin) { e.tipo = Tipo.Procesando; e.progreso = Frac(t, prev.inicio, prev.fin); }
                    else e.tipo = Tipo.Espera;
                    return e;
                }
                if (t < a.inicio)
                {
                    e.tipo = Tipo.Viaje;
                    e.etapa = k;
                    e.maquinaDesde = k == 0 ? -1 : p.etapas[k - 1].maquinaId;
                    e.maquinaHasta = a.maquinaId;
                    e.progreso = Frac(t, sal, a.inicio);
                    return e;
                }
            }
            // ya llegó a la última etapa
            Assignment u = p.etapas[n - 1];
            if (t < u.fin)
            {
                e.tipo = Tipo.Procesando; e.etapa = n - 1;
                e.maquinaDesde = e.maquinaHasta = u.maquinaId;
                e.progreso = Frac(t, u.inicio, u.fin);
                return e;
            }
            if (t < p.llegadaSalida)
            {
                e.tipo = Tipo.ViajeSalida; e.etapa = n - 1;
                e.maquinaDesde = u.maquinaId; e.maquinaHasta = -1;
                e.progreso = Frac(t, u.fin, p.llegadaSalida);
                return e;
            }
            e.tipo = Tipo.Salida; e.etapa = n - 1;
            return e;
        }

        /// <summary>Etapa que procesa la máquina en t (−1 = ociosa).</summary>
        public int EtapaEnMaquina(int maquinaId, double t)
        {
            List<Assignment> l;
            if (!porMaquina.TryGetValue(maquinaId, out l)) return -1;
            foreach (Assignment a in l)
            {
                if (a.inicio > t) break;
                if (t < a.fin) return (int)a.etapa;
            }
            return -1;
        }

        public int TareasDeMaquina(int maquinaId)
        {
            List<Assignment> l;
            return porMaquina.TryGetValue(maquinaId, out l) ? l.Count : 0;
        }

        /// <summary>Σ energia · clamp01((t − inicio)/(fin − inicio)).</summary>
        public double Energia(double t)
        {
            double s = 0;
            foreach (Assignment a in todas) s += a.energia * Frac(t, a.inicio, a.fin);
            return s;
        }

        public double EnergiaMaquina(int maquinaId, double t)
        {
            List<Assignment> l;
            if (!porMaquina.TryGetValue(maquinaId, out l)) return 0;
            double s = 0;
            foreach (Assignment a in l) s += a.energia * Frac(t, a.inicio, a.fin);
            return s;
        }

        public double EnergiaMaquinaTotal(int maquinaId)
        {
            return EnergiaMaquina(maquinaId, double.MaxValue);
        }

        /// <summary>Piezas que ya llegaron a la salida.</summary>
        public int PiezasTerminadas(double t)
        {
            int c = 0;
            foreach (Pieza p in piezas) if (p.etapas.Count > 0 && t >= p.llegadaSalida) c++;
            return c;
        }

        public static float Frac(double t, double a, double b)
        {
            if (b <= a) return t >= b ? 1f : 0f;
            double f = (t - a) / (b - a);
            return (float)(f < 0 ? 0 : (f > 1 ? 1 : f));
        }
    }
}
