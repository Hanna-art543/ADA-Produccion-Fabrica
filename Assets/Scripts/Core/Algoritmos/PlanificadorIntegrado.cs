using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Fabrica.Core
{
    // Dos pipelines que integran Divide y Vencerás + Goloso + Programación Dinámica.
    public static class PlanificadorIntegrado
    {
        public const string NombreA = "Integrado 1: DyV→Goloso→PD";
        public const string NombreB = "Integrado 2: PD reparte E";

        // ===================================================================
        // BLOQUE 1 · INTEGRADO 1: DyV (con Goloso) -> PD
        //   1) DyV y Goloso, cada uno con su λ calibrado, generan soluciones semilla.
        //   2) Se elige la mejor semilla.
        //   3) La PD parte de esa semilla y solo explora planes que no la empeoren.
        // ===================================================================
        public static PlanResult Resolver(Escenario esc, int iterCalib, int resolucionPD)
        {
            var sw = Stopwatch.StartNew();
            int[] sol = Pipeline1(esc, iterCalib, resolucionPD, out string nota);
            sw.Stop();
            var r = Simulador.Simular(esc, sol, NombreA);
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.notas = nota;
            return r;
        }

        private static int[] Pipeline1(Escenario esc, int iterCalib, int resolucionPD, out string nota)
        {
            // Pasos 1 y 2: semillas de DyV y de Goloso, y nos quedamos con la mejor
            var calDv = CalibracionLambda.Calibrar(esc, lam => DivideVenceras.Asignar(esc, lam), iterCalib);
            var calGo = CalibracionLambda.Calibrar(esc, lam => Goloso.Asignar(esc, lam), iterCalib);
            var rDv = Simulador.Simular(esc, calDv.asignacion, "semilla DyV");
            var rGo = Simulador.Simular(esc, calGo.asignacion, "semilla Goloso");

            bool usaDv = EsMejor(rDv, rGo);
            int[] semilla = usaDv ? calDv.asignacion : calGo.asignacion;
            var rSem = usaDv ? rDv : rGo;

            // Paso 3: la PD intenta mejorar la semilla (si no puede, se queda la semilla)
            int[] refinada = RefinarPD(esc, rSem.costoTotal, resolucionPD);
            int[] final = semilla;
            string origen = usaDv ? "semilla DyV" : "semilla Goloso";
            if (refinada != null)
            {
                var rPd = Simulador.Simular(esc, refinada, "PD refinada");
                if (EsMejor(rPd, rSem)) { final = refinada; origen = "PD mejoró la semilla"; }
            }
            nota = string.Format(CultureInfo.InvariantCulture,
                                 "Resultado final: {0}. Costo semilla = {1:F2}.", origen, rSem.costoTotal);
            return final;
        }

        // Un plan factible siempre gana a uno infactible; si empatan, gana el de menor costo
        private static bool EsMejor(PlanResult a, PlanResult b)
        {
            if (a.factible != b.factible) return a.factible;
            return a.costoTotal < b.costoTotal - 1e-9;
        }

        // ===================================================================
        // BLOQUE 2 · PD que refina la semilla (usada por el Integrado 1)
        //   dp[t][e] = menor costo tras t tareas usando e unidades de energía.
        //   Se poda todo estado cuyo costo ya supera la semilla (+5 %).
        // ===================================================================
        private static int[] RefinarPD(Escenario esc, double costoSemilla, int E)
        {
            int n = esc.NumTareas, M = esc.NumMaquinas;
            int[] orden = esc.OrdenTareas();
            double escala = E / esc.energiaMax;
            double cota = costoSemilla * 1.05 + 1e-9;

            // Energía discretizada (redondeo hacia arriba: nunca excede E_max)
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
                bool primera = esc.jobs[j].etapa == Etapa.Corte;
                decision[t] = new sbyte[E + 1];
                sig.Limpiar();

                for (int e = 0; e <= E; e++)
                {
                    if (double.IsPositiveInfinity(actual.costo[e])) continue;
                    double listo = primera ? 0 : actual.listo[e];
                    double[] libre = actual.libre[e];

                    foreach (int k in esc.Validas(j))
                    {
                        int e2 = e + ed[t][k];
                        if (e2 > E) continue;
                        double fin = Math.Max(listo, libre[k]) + esc.Tiempo(j, k);
                        double mk = Math.Max(actual.makespan[e], fin);
                        double en = actual.energia[e] + esc.Energia(j, k);
                        double c = esc.Costo(mk, en);
                        if (c > cota) continue; // poda por la semilla
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

            // Mejor estado final y reconstrucción hacia atrás
            int mejorE = -1;
            for (int e = 0; e <= E; e++)
                if (!double.IsPositiveInfinity(actual.costo[e]) &&
                    (mejorE < 0 || actual.costo[e] < actual.costo[mejorE]))
                    mejorE = e;
                if (mejorE < 0) return null;

                int[] maq = new int[n];
            int ee = mejorE;
            for (int t = n - 1; t >= 0; t--)
            {
                int k = decision[t][ee];
                maq[orden[t]] = k;
                ee -= ed[t][k];
            }
            return maq;
        }

        // ===================================================================
        // BLOQUE 3 · INTEGRADO 2: etapas (DyV) + Goloso con varios λ + PD reparte la energía
        //   Si no logra un plan factible, usa el resultado del Integrado 1 como respaldo.
        // ===================================================================
        public static PlanResult ResolverB(Escenario esc, int resolucionPD, int numLambdas)
        {
            var sw = Stopwatch.StartNew();
            int[] sol = EtapasPD(esc, resolucionPD, Math.Max(3, numLambdas), 30, out string nota);
            PlanResult r = sol != null ? Simulador.Simular(esc, sol, NombreB) : null;
            if (r == null || !r.factible)
            {
                sol = Pipeline1(esc, 30, resolucionPD, out string nota1);
                r = Simulador.Simular(esc, sol, NombreB);
                nota = "Sin plan factible por etapas; se usa el resultado del Integrado 1. " + nota1;
            }
            sw.Stop();
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.notas = nota;
            return r;
        }

        private static int[] EtapasPD(Escenario esc, int E, int numLambdas, int iterCalib, out string nota)
        {
            int P = esc.numPiezas, M = esc.NumMaquinas, S = Etapas.Cantidad;
            double escala = E / esc.energiaMax;

            // --- Rejilla de λ para el Goloso de cada etapa ---
            // 0 = solo rapidez; 1e6 = casi mínima energía; alrededor del λ calibrado; y una escala logarítmica
            var calGo = CalibracionLambda.Calibrar(esc, lam => Goloso.Asignar(esc, lam), iterCalib);
            var listaL = new List<double> { 0, 1e6, calGo.lambda * 0.5, calGo.lambda, calGo.lambda * 2 };
            for (int i = 0; i < numLambdas; i++)
                listaL.Add(0.01 * Math.Pow(1e5, (double)i / Math.Max(1, numLambdas - 1)));
            double[] lambdas = listaL.ToArray();

            var act = new CapaEtapas(E, M, P);
            var sig = new CapaEtapas(E, M, P);
            act.Limpiar();
            act.costo[0] = 0;

            int[][][] maqEtapa = new int[S][][]; // [etapa][energía] -> máquina por pieza
            int[][] previo = new int[S][];       // energía antes de la etapa (para reconstruir)

            // --- PD sobre las etapas: dp[s][e] ---
            for (int s = 0; s < S; s++)
            {
                maqEtapa[s] = new int[E + 1][];
                previo[s] = new int[E + 1];
                sig.Limpiar();

                for (int e = 0; e <= E; e++)
                {
                    if (double.IsPositiveInfinity(act.costo[e])) continue;

                    // Piezas en orden de liberación (fin de la etapa anterior)
                    var piezas = new List<int>(P);
                    for (int p = 0; p < P; p++) piezas.Add(p);
                    double[] finPrev = act.finPieza[e];
                    piezas.Sort((a, b) =>
                    {
                        int c = finPrev[a].CompareTo(finPrev[b]);
                        return c != 0 ? c : esc.RangoPieza(a).CompareTo(esc.RangoPieza(b));
                    });

                    // Cada λ produce un plan candidato de esta etapa (Goloso)
                    foreach (double lam in lambdas)
                    {
                        double[] libre = (double[])act.libre[e].Clone();
                        double[] fin = (double[])act.finPieza[e].Clone();
                        int[] maqs = new int[P];
                        double enEtapa = 0, mk = act.makespan[e];

                        foreach (int p in piezas)
                        {
                            int j = Escenario.IdTarea(p, s);
                            int mejor = -1;
                            double mejorCrit = double.MaxValue, mejorE = double.MaxValue, mejorFin = 0;
                            foreach (int i in esc.Validas(j))
                            {
                                double en = esc.Energia(j, i);
                                double f = Math.Max(fin[p], libre[i]) + esc.Tiempo(j, i);
                                double crit = f + lam * en;
                                if (crit < mejorCrit || (crit == mejorCrit && en < mejorE))
                                {
                                    mejor = i; mejorCrit = crit; mejorE = en; mejorFin = f;
                                }
                            }
                            maqs[p] = mejor;
                            libre[mejor] = mejorFin;
                            fin[p] = mejorFin;
                            enEtapa += mejorE;
                            if (mejorFin > mk) mk = mejorFin;
                        }

                        // La PD guarda el mejor candidato para cada nivel de energía acumulada
                        int ed = (int)Math.Ceiling(enEtapa * escala - 1e-9);
                        int e2 = e + ed;
                        if (e2 > E) continue; // respeta E_max

                        double enTotal = act.energia[e] + enEtapa;
                        double costo = esc.Costo(mk, enTotal);
                        if (costo < sig.costo[e2] - 1e-12)
                        {
                            sig.costo[e2] = costo;
                            sig.makespan[e2] = mk;
                            sig.energia[e2] = enTotal;
                            Array.Copy(libre, sig.libre[e2], M);
                            Array.Copy(fin, sig.finPieza[e2], P);
                            maqEtapa[s][e2] = maqs;
                            previo[s][e2] = e;
                        }
                    }
                }
                var tmp = act; act = sig; sig = tmp;
            }

            // --- Mejor estado final y reconstrucción ---
            int mejorEnergia = -1;
            for (int e = 0; e <= E; e++)
                if (!double.IsPositiveInfinity(act.costo[e]) &&
                    (mejorEnergia < 0 || act.costo[e] < act.costo[mejorEnergia]))
                    mejorEnergia = e;

                if (mejorEnergia < 0)
                {
                    nota = "Sin estado factible con esta resolución.";
                    return null;
                }

                int[] maq = new int[esc.NumTareas];
                int ee = mejorEnergia;
                for (int s = S - 1; s >= 0; s--)
                {
                    int[] m = maqEtapa[s][ee];
                    for (int p = 0; p < P; p++) maq[Escenario.IdTarea(p, s)] = m[p];
                    ee = previo[s][ee];
                }
                nota = "PD repartió la energía entre etapas (" + lambdas.Length + " valores de λ por etapa); usadas "
                + mejorEnergia + " de " + E + " unidades.";
                return maq;
        }

        // ===================================================================
        // BLOQUE 4 · Tablas auxiliares de la PD (una por nivel de energía)
        // ===================================================================
        private class Capa
        {
            public readonly double[] costo, makespan, energia, listo;
            public readonly double[][] libre;
            public Capa(int E, int M)
            {
                costo = new double[E + 1]; makespan = new double[E + 1];
                energia = new double[E + 1]; listo = new double[E + 1];
                libre = new double[E + 1][];
                for (int e = 0; e <= E; e++) libre[e] = new double[M];
            }
            public void Limpiar()
            {
                for (int e = 0; e < costo.Length; e++)
                {
                    costo[e] = double.PositiveInfinity;
                    makespan[e] = 0; energia[e] = 0; listo[e] = 0;
                    Array.Clear(libre[e], 0, libre[e].Length);
                }
            }
        }

        private class CapaEtapas
        {
            public readonly double[] costo, makespan, energia;
            public readonly double[][] libre, finPieza;
            public CapaEtapas(int E, int M, int P)
            {
                costo = new double[E + 1]; makespan = new double[E + 1]; energia = new double[E + 1];
                libre = new double[E + 1][]; finPieza = new double[E + 1][];
                for (int e = 0; e <= E; e++)
                {
                    libre[e] = new double[M];
                    finPieza[e] = new double[P];
                }
            }
            public void Limpiar()
            {
                for (int e = 0; e < costo.Length; e++)
                {
                    costo[e] = double.PositiveInfinity;
                    makespan[e] = 0; energia[e] = 0;
                    Array.Clear(libre[e], 0, libre[e].Length);
                    Array.Clear(finPieza[e], 0, finPieza[e].Length);
                }
            }
        }
    }
}
