using System;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Programación Dinámica sobre energía discretizada.
    //   dp[i][e] = menor costo estimado (α·makespan + β·energía) al asignar las primeras i tareas
    //              usando exactamente e unidades de energía discreta (ê_ij = ceil(e_ij · escala)).
    //   Transición: dp[i+1][e + ê_ik] <- dp[i][e] eligiendo la máquina k para la tarea i.
    //   Cada estado guarda además las cargas de las máquinas (línea de tiempo serial) para
    //   estimar el makespan, que NO es aditivo. Por eso la PD es exacta en energía
    //   (nunca excede E_max) pero aproximada en makespan.
    //   decision[i][e] guarda la máquina elegida para reconstruir la solución.
    public static class ProgramacionDinamica
    {
        public static PlanResult Resolver(Escenario esc, int resolucion)
        {
            var sw = Stopwatch.StartNew();
            int n = esc.NumTareas, M = esc.NumMaquinas;
            int[] orden = esc.OrdenTareas(); // pieza por pieza (LPT), etapas en orden
            int E = resolucion;
            double escala = E / esc.energiaMax;

            // Energía discretizada (redondeo hacia arriba => Σê ≤ E garantiza Σe ≤ E_max)
            int[][] ed = new int[n][];
            for (int t = 0; t < n; t++)
            {
                ed[t] = new int[M];
                foreach (int k in esc.Validas(orden[t]))
                    ed[t][k] = (int)Math.Ceiling(esc.Energia(orden[t], k) * escala - 1e-9);
            }

            sbyte[][] decision = new sbyte[n][];
            var actual = new Capa(E, M);
            var sig = new Capa(E, M);
            actual.Limpiar();
            actual.costo[0] = 0;

            for (int t = 0; t < n; t++)
            {
                int j = orden[t];
                bool primeraEtapa = esc.jobs[j].etapa == Etapa.Corte;
                decision[t] = new sbyte[E + 1];
                sig.Limpiar();

                for (int e = 0; e <= E; e++)
                {
                    if (double.IsPositiveInfinity(actual.costo[e])) continue;
                    double listo = primeraEtapa ? 0 : actual.listo[e];
                    double[] libre = actual.libre[e];

                    foreach (int k in esc.Validas(j))
                    {
                        int e2 = e + ed[t][k];
                        if (e2 > E) continue; // respeta E_max
                        double fin = Math.Max(listo, libre[k]) + esc.Tiempo(j, k);
                        double mk = Math.Max(actual.makespan[e], fin);
                        double en = actual.energia[e] + esc.Energia(j, k);
                        double c = esc.Costo(mk, en);
                        if (c < sig.costo[e2] - 1e-12)
                        {
                            sig.costo[e2] = c;
                            sig.makespan[e2] = mk;
                            sig.energia[e2] = en;
                            sig.listo[e2] = fin;
                            Array.Copy(libre, sig.libre[e2], M);
                            sig.libre[e2][k] = fin;
                            decision[t][e2] = (sbyte)k;
                        }
                    }
                }
                var tmp = actual; actual = sig; sig = tmp;
            }

            // Mejor estado final
            int mejorE = -1;
            for (int e = 0; e <= E; e++)
                if (!double.IsPositiveInfinity(actual.costo[e]) && (mejorE < 0 || actual.costo[e] < actual.costo[mejorE]))
                    mejorE = e;

            int[] maq = new int[n];
            string notas;
            if (mejorE < 0)
            {
                // Solo ocurre si la discretización no deja ninguna solución: se usa mínima energía.
                for (int j = 0; j < n; j++) maq[j] = Goloso.MaquinaMinEnergia(esc, j);
                notas = "Sin estado factible con la resolución dada; se usa la asignación de mínima energía.";
            }
            else
            {
                // Reconstrucción hacia atrás con la tabla de decisiones
                int e = mejorE;
                for (int t = n - 1; t >= 0; t--)
                {
                    int k = decision[t][e];
                    maq[orden[t]] = k;
                    e -= ed[t][k];
                }
                notas = "Resolución " + E + " unidades de energía; usadas " + mejorE + ".";
            }
            sw.Stop();

            var r = Simulador.Simular(esc, maq, "Prog. Dinámica");
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.notas = notas;
            return r;
        }

        // Una fila dp[i][*] con el estado asociado a cada nivel de energía.
        private class Capa
        {
            public readonly double[] costo, makespan, energia, listo;
            public readonly double[][] libre;

            public Capa(int E, int M)
            {
                costo = new double[E + 1];
                makespan = new double[E + 1];
                energia = new double[E + 1];
                listo = new double[E + 1];
                libre = new double[E + 1][];
                for (int e = 0; e <= E; e++) libre[e] = new double[M];
            }

            public void Limpiar()
            {
                for (int e = 0; e < costo.Length; e++)
                {
                    costo[e] = double.PositiveInfinity;
                    makespan[e] = 0;
                    energia[e] = 0;
                    listo[e] = 0;
                    Array.Clear(libre[e], 0, libre[e].Length);
                }
            }
        }
    }
}
