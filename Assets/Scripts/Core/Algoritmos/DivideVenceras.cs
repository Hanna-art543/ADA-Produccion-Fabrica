using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Divide y Vencerás en dos niveles:
    //  1) Por etapas: Corte -> Ensamblaje -> Empaquetado. Cada etapa es un subproblema;
    //     los tiempos de liberación de una etapa son los fines de la etapa anterior.
    //  2) Dentro de cada etapa: se divide recursivamente el grupo de máquinas capaces en dos
    //     mitades de capacidad parecida y se reparten las tareas (LPT) entre ambas con el
    //     criterio goloso (fin estimado + λ·energía). Caso base: una sola máquina.
    //     Como los subgrupos son disjuntos, combinar es unir las asignaciones y secuenciarlas.
    // Al final se repara la energía si se pasó de E_max.
    public static class DivideVenceras
    {
        public static PlanResult Resolver(Escenario esc)
        {
            var sw = Stopwatch.StartNew();
            int[] maq = Asignar(esc, esc.lambda, out int reparaciones);
            sw.Stop();
            var r = Simulador.Simular(esc, maq, "Divide y Vencerás");
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            if (reparaciones > 0) r.notas = "Reparación de energía: " + reparaciones + " movimientos.";
            return r;
        }

        public static int[] Asignar(Escenario esc, double lambda, out int reparaciones)
        {
            int n = esc.NumTareas, M = esc.NumMaquinas, P = esc.numPiezas;
            int[] maq = new int[n];
            double[] libre = new double[M];     // se arrastra entre etapas (máquinas compartidas)
            double[] finPieza = new double[P];  // liberación para la etapa siguiente

            for (int s = 0; s < Etapas.Cantidad; s++)
            {
                // Tareas de la etapa en orden LPT
                var tareas = new List<int>(P);
                for (int p = 0; p < P; p++) tareas.Add(Escenario.IdTarea(p, s));
                tareas.Sort((a, b) =>
                {
                    int c = esc.jobs[b].trabajo.CompareTo(esc.jobs[a].trabajo);
                    return c != 0 ? c : a.CompareTo(b);
                });

                var grupo = new List<int>();
                for (int i = 0; i < M; i++)
                    if (esc.maquinas[i].PuedeHacer((Etapa)s)) grupo.Add(i);

                Dividir(esc, tareas, grupo, libre, lambda, maq);
                CombinarEtapa(esc, s, maq, libre, finPieza);
            }

            reparaciones = ReparacionEnergia.Reparar(esc, maq);
            return maq;
        }

        // Reparte 'tareas' entre las máquinas de 'grupo' (recursivo).
        private static void Dividir(Escenario esc, List<int> tareas, List<int> grupo,
                                    double[] libre, double lambda, int[] maq)
        {
            if (tareas.Count == 0) return;
            if (grupo.Count == 1)
            {
                foreach (int t in tareas) maq[t] = grupo[0];
                return;
            }

            // Dividir máquinas en dos mitades de capacidad parecida (reparto en serpiente)
            var ordenadas = new List<int>(grupo);
            ordenadas.Sort((a, b) =>
            {
                int c = esc.maquinas[b].velocidad.CompareTo(esc.maquinas[a].velocidad);
                return c != 0 ? c : a.CompareTo(b);
            });
            var g1 = new List<int>();
            var g2 = new List<int>();
            for (int k = 0; k < ordenadas.Count; k++)
            {
                if (k % 4 == 0 || k % 4 == 3) g1.Add(ordenadas[k]);
                else g2.Add(ordenadas[k]);
            }

            // Capacidad (Σv), carga inicial en unidades de trabajo y energía por unidad de trabajo (ΣP/Σv)
            Resumir(esc, g1, libre, out double v1, out double carga1, out double tasa1);
            Resumir(esc, g2, libre, out double v2, out double carga2, out double tasa2);

            var t1 = new List<int>();
            var t2 = new List<int>();
            foreach (int t in tareas)
            {
                double w = esc.jobs[t].trabajo;
                double c1 = (carga1 + w) / v1 + lambda * w * tasa1;
                double c2 = (carga2 + w) / v2 + lambda * w * tasa2;
                if (c1 <= c2) { t1.Add(t); carga1 += w; }
                else { t2.Add(t); carga2 += w; }
            }

            Dividir(esc, t1, g1, libre, lambda, maq);
            Dividir(esc, t2, g2, libre, lambda, maq);
        }

        private static void Resumir(Escenario esc, List<int> g, double[] libre,
                                    out double v, out double carga, out double tasa)
        {
            v = 0; carga = 0;
            double pot = 0;
            foreach (int i in g)
            {
                Machine m = esc.maquinas[i];
                v += m.velocidad;
                pot += m.potencia;
                carga += libre[i] * m.velocidad; // tiempo ocupado convertido a trabajo
            }
            tasa = pot / v;
        }

        // Combinar: secuencia la etapa respetando liberaciones (fin de la etapa anterior).
        private static void CombinarEtapa(Escenario esc, int s, int[] maq, double[] libre, double[] finPieza)
        {
            int P = esc.numPiezas;
            var piezas = new List<int>(P);
            for (int p = 0; p < P; p++) piezas.Add(p);
            piezas.Sort((a, b) =>
            {
                int c = finPieza[a].CompareTo(finPieza[b]);
                return c != 0 ? c : esc.RangoPieza(a).CompareTo(esc.RangoPieza(b));
            });
            foreach (int p in piezas)
            {
                int j = Escenario.IdTarea(p, s);
                int i = maq[j];
                double fin = Math.Max(finPieza[p], libre[i]) + esc.Tiempo(j, i);
                libre[i] = fin;
                finPieza[p] = fin;
            }
        }
    }
}
