using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Fabrica.Core
{
    // ===========================================================================================
    // INTEGRADO (D&V + Goloso + PD): un solo algoritmo que combina los tres paradigmas.
    //
    //  1) DIVIDE (Divide y Vencerás): el problema se parte en 3 subproblemas, uno por etapa
    //     (Corte → Ensamblaje → Empaquetado). Cada subproblema decide la máquina de las P tareas
    //     de esa etapa, solo entre las máquinas capaces de hacerla. Las etapas se resuelven en orden:
    //     los fines de una etapa son los tiempos de liberación de la siguiente.
    //
    //  2) GOLOSO: para cada etapa se arma un menú de opciones (asignaciones parciales de esa etapa):
    //       - L_λ pasadas golosas (piezas por orden de liberación, desempate LPT; cada tarea a la máquina
    //         que minimiza fin + λ·energía) con λ = 0, 10^6, λ_cal·{0.5, 1, 2} y una escala logarítmica
    //         (λ_cal = λ calibrado del Goloso global);
    //       - la parte de esa etapa de los planes completos Goloso, D&V y PD.
    //     Las opciones repetidas (misma máquina para cada pieza) se eliminan.
    //
    //  3) PD (mochila de elección múltiple sobre la energía):
    //       dp[s][e] = menor costo estimado α·makespan + β·energía tras las etapas 0..s gastando
    //       exactamente e unidades de energía discreta (E_max ↦ R unidades, redondeo hacia arriba,
    //       así Σ ê ≤ R ⇒ energía ≤ E_max). Cada celda guarda el estado (máquinas libres, fin de cada
    //       pieza) del mejor camino, y las opciones del Goloso se evalúan a partir de ese estado.
    //     Como el makespan no es aditivo, la PD da una estimación. Por eso se reconstruye la mejor
    //     combinación de CADA nivel de energía final (hasta K), se evalúa cada una con el Simulador
    //     real (el mismo de todos los métodos) junto con los 3 planes completos Goloso, D&V y PD,
    //     y se elige la factible de menor costo.
    //
    //  Garantía: los planes Goloso, D&V y PD siempre son candidatos y todos se evalúan con el mismo
    //  Simulador, así que costo(Integrado) ≤ min(costo Goloso, D&V, PD).
    //
    //  Complejidad (S = 3 etapas, P piezas, m máquinas, n = S·P tareas, L opciones por etapa,
    //  R = resolución de energía, K combinaciones evaluadas, k = iteraciones de la calibración de λ):
    //    Tiempo:  O(k·(n log n + n·m))          calibración de λ_cal (Goloso)
    //           + O(S·R·(P log P + L·P·m))      PD: R estados por etapa × L opciones × pasada golosa
    //           + O(K·(R + S·P + n·P))          ordenar/reconstruir + Simular() de cada candidata
    //    Espacio: O(S·R·P + R·(m + P))          decisiones por etapa + dos capas de estados
    // ===========================================================================================
    public static class PlanificadorIntegrado
    {
        public const string Nombre = "Integrado (D&V + Goloso + PD)";

        public static PlanResult Resolver(Escenario esc, PlanResult goloso, PlanResult dyv, PlanResult pd,
                                          int resolucionPD, int numLambdas, int maxCombinaciones, int iterCalib)
        {
            var sw = Stopwatch.StartNew();
            int S = Etapas.Cantidad, P = esc.numPiezas, M = esc.NumMaquinas, n = esc.NumTareas;
            int E = resolucionPD;
            double escala = E / esc.energiaMax;
            int[] rango = new int[P];
            for (int p = 0; p < P; p++) rango[p] = esc.RangoPieza(p);

            // ---------------- 2) Menú del Goloso: λ + partes de los planes completos ----------------
            var calGo = CalibracionLambda.Calibrar(esc, lam => Goloso.Asignar(esc, lam), iterCalib);
            var listaL = new List<double> { 0, 1e6, calGo.lambda * 0.5, calGo.lambda, calGo.lambda * 2 };
            for (int i = 0; i < numLambdas; i++)
                listaL.Add(0.01 * Math.Pow(1e5, (double)i / Math.Max(1, numLambdas - 1)));
            var lambdas = new List<double>();
            foreach (double l in listaL)
                if (!double.IsInfinity(l) && !double.IsNaN(l) && !lambdas.Contains(l)) lambdas.Add(l);

            var completos = new[] { goloso, dyv, pd };
            var nombresCompletos = new[] { "Goloso", "D&V", "PD" };
            // fijas[s][f] = máquina de cada pieza en la etapa s según el plan completo f
            var fijas = new int[S][][];
            for (int s = 0; s < S; s++)
            {
                fijas[s] = new int[completos.Length][];
                for (int f = 0; f < completos.Length; f++)
                {
                    int[] maqF = completos[f].MaquinaPorTarea();
                    fijas[s][f] = new int[P];
                    for (int p = 0; p < P; p++) fijas[s][f][p] = maqF[Escenario.IdTarea(p, s)];
                }
            }

            // Etiquetas de las opciones: primero los λ, luego las 3 partes fijas
            int L = lambdas.Count + completos.Length;
            var etiquetas = new string[L];
            for (int o = 0; o < lambdas.Count; o++)
                etiquetas[o] = "λ=" + lambdas[o].ToString("G4", CultureInfo.InvariantCulture);
            for (int f = 0; f < completos.Length; f++) etiquetas[lambdas.Count + f] = "parte de " + nombresCompletos[f];

            // ---------------- 3) PD sobre las etapas: dp[s][e] ----------------
            var act = new Capa(E, M, P);
            var sig = new Capa(E, M, P);
            act.Limpiar();
            act.costo[0] = 0;

            var maqEtapa = new int[S][][];  // [etapa][energía final de la etapa] -> máquina por pieza
            var previo = new int[S][];      // energía acumulada antes de la etapa
            var fuente = new int[S][];      // índice de la opción elegida (etiqueta)
            var distintasPorEtapa = new double[S];

            var piezas = new int[P];
            var vistas = new List<int[]>(L);
            for (int s = 0; s < S; s++)
            {
                maqEtapa[s] = new int[E + 1][];
                previo[s] = new int[E + 1];
                fuente[s] = new int[E + 1];
                sig.Limpiar();
                long estados = 0, distintas = 0;

                for (int e = 0; e <= E; e++)
                {
                    if (double.IsPositiveInfinity(act.costo[e])) continue;
                    estados++;
                    double[] finPrev = act.finPieza[e];
                    for (int p = 0; p < P; p++) piezas[p] = p;
                    // Piezas por orden de liberación (fin de la etapa anterior); desempate LPT
                    Array.Sort(piezas, (a, b) =>
                    {
                        int c = finPrev[a].CompareTo(finPrev[b]);
                        return c != 0 ? c : rango[a].CompareTo(rango[b]);
                    });

                    vistas.Clear();
                    for (int o = 0; o < L; o++)
                    {
                        int[] maqs = new int[P];
                        double[] libre = (double[])act.libre[e].Clone();
                        double[] fin = (double[])finPrev.Clone();
                        double enEtapa = 0, mk = act.makespan[e];
                        bool esLambda = o < lambdas.Count;
                        double lam = esLambda ? lambdas[o] : 0;
                        int[] fija = esLambda ? null : fijas[s][o - lambdas.Count];

                        foreach (int p in piezas)
                        {
                            int j = Escenario.IdTarea(p, s);
                            int elegida;
                            if (esLambda)
                            {
                                elegida = -1;
                                double mejorCrit = double.MaxValue, mejorE = double.MaxValue;
                                foreach (int i in esc.Validas(j))
                                {
                                    double en = esc.Energia(j, i);
                                    double f = Math.Max(fin[p], libre[i]) + esc.Tiempo(j, i);
                                    double crit = f + lam * en;
                                    if (crit < mejorCrit || (crit == mejorCrit && en < mejorE))
                                    {
                                        elegida = i; mejorCrit = crit; mejorE = en;
                                    }
                                }
                            }
                            else elegida = fija[p];

                            double fFin = Math.Max(fin[p], libre[elegida]) + esc.Tiempo(j, elegida);
                            maqs[p] = elegida;
                            libre[elegida] = fFin;
                            fin[p] = fFin;
                            enEtapa += esc.Energia(j, elegida);
                            if (fFin > mk) mk = fFin;
                        }

                        // Eliminar duplicados: misma máquina para cada pieza => misma opción
                        if (Repetida(vistas, maqs)) continue;
                        vistas.Add(maqs);
                        distintas++;

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
                            fuente[s][e2] = o;
                        }
                    }
                }
                distintasPorEtapa[s] = estados > 0 ? (double)distintas / estados : 0;
                var tmp = act; act = sig; sig = tmp;
            }

            // ---------------- Candidatas: mejor combinación de cada nivel de energía final ----------------
            var niveles = new List<int>();
            for (int e = 0; e <= E; e++)
                if (!double.IsPositiveInfinity(act.costo[e])) niveles.Add(e);
            var costosFinales = act.costo;
            niveles.Sort((a, b) =>
            {
                int c = costosFinales[a].CompareTo(costosFinales[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            if (niveles.Count > maxCombinaciones) niveles.RemoveRange(maxCombinaciones, niveles.Count - maxCombinaciones);

            // Los 3 planes completos van primero: ante un empate, se reporta el método solo.
            int[] mejor = null;
            double mejorCosto = double.MaxValue;
            string mejorOrigen = null;
            var vistosPlanes = new HashSet<string>();
            int evaluadas = 0;

            for (int f = 0; f < completos.Length; f++)
            {
                int[] maqF = completos[f].MaquinaPorTarea();
                if (!vistosPlanes.Add(Firma(maqF))) continue;
                evaluadas++;
                Considerar(esc, maqF, "plan completo " + nombresCompletos[f], ref mejor, ref mejorCosto, ref mejorOrigen);
            }

            foreach (int eFinal in niveles)
            {
                int[] maq = new int[n];
                var fuentes = new string[S];
                int ee = eFinal;
                for (int s = S - 1; s >= 0; s--)
                {
                    int[] m = maqEtapa[s][ee];
                    for (int p = 0; p < P; p++) maq[Escenario.IdTarea(p, s)] = m[p];
                    fuentes[s] = etiquetas[fuente[s][ee]];
                    ee = previo[s][ee];
                }
                if (!vistosPlanes.Add(Firma(maq))) continue;
                evaluadas++;
                string origen = string.Format("Corte: {0}; Ensamblaje: {1}; Empaquetado: {2}", fuentes[0], fuentes[1], fuentes[2]);
                Considerar(esc, maq, origen, ref mejor, ref mejorCosto, ref mejorOrigen);
            }

            if (mejor == null)
            {
                // No debería ocurrir (Goloso, D&V y PD son factibles); se devuelve el plan PD
                mejor = pd.MaquinaPorTarea();
                mejorOrigen = "respaldo: plan completo PD (ninguna candidata factible)";
            }
            sw.Stop();

            var r = Simulador.Simular(esc, mejor, Nombre);
            // El tiempo incluye el de Goloso, D&V y PD, que el Integrado necesita como entrada.
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds
                                + goloso.tiempoEjecucionMs + dyv.tiempoEjecucionMs + pd.tiempoEjecucionMs;

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Opciones por etapa: {0} ({1} valores de λ + partes de Goloso, D&V y PD); distintas en promedio: " +
                "Corte {2:F1}, Ensamblaje {3:F1}, Empaquetado {4:F1}. ",
                L, lambdas.Count, distintasPorEtapa[0], distintasPorEtapa[1], distintasPorEtapa[2]);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Combinaciones evaluadas con Simular(): {0} ({1} niveles de energía de la PD + 3 planes completos, sin repetidos). ",
                evaluadas, niveles.Count);
            sb.Append("Elegida: ").Append(mejorOrigen).Append('.');
            r.notas = sb.ToString();
            return r;
        }

        private static void Considerar(Escenario esc, int[] maq, string origen,
                                       ref int[] mejor, ref double mejorCosto, ref string mejorOrigen)
        {
            bool valido = Simulador.Evaluar(esc, maq, out double mk, out double en);
            if (!valido || !Simulador.EsFactible(esc, en)) return;
            double c = esc.Costo(mk, en);
            if (c < mejorCosto - 1e-9)
            {
                mejor = (int[])maq.Clone();
                mejorCosto = c;
                mejorOrigen = origen;
            }
        }

        private static bool Repetida(List<int[]> vistas, int[] maqs)
        {
            foreach (int[] v in vistas)
            {
                bool igual = true;
                for (int i = 0; i < v.Length && igual; i++) igual = v[i] == maqs[i];
                if (igual) return true;
            }
            return false;
        }

        private static string Firma(int[] maq)
        {
            var sb = new StringBuilder(maq.Length * 2);
            foreach (int m in maq) sb.Append(m).Append(',');
            return sb.ToString();
        }

        // Una capa de la PD: un estado por nivel de energía acumulada.
        private class Capa
        {
            public readonly double[] costo, makespan, energia;
            public readonly double[][] libre, finPieza;

            public Capa(int E, int M, int P)
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
