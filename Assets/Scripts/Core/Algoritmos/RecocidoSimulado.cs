using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Recocido Simulado sobre asignaciones.
    //   Vecinos: mover una tarea a otra máquina válida, o intercambiar las máquinas de dos tareas.
    //   Objetivo: costo + ρ·max(0, energía − E_max)  (penaliza exceder E_max).
    //   Enfriamiento geométrico de T0 = 5 % del costo inicial a 0.01 % del costo inicial.
    //   Se devuelve la mejor solución FACTIBLE encontrada.
    public static class RecocidoSimulado
    {
        public static PlanResult Resolver(Escenario esc, int[] inicial, int iteraciones, ulong semilla)
        {
            var sw = Stopwatch.StartNew();
            var rng = new Rng(semilla);
            int n = esc.NumTareas;
            int[] x = (int[])inicial.Clone();

            Simulador.Evaluar(esc, x, out double mk, out double en);
            double costo0 = esc.Costo(mk, en);
            double rho = 10.0 * costo0 / esc.energiaMax;
            double f = Objetivo(esc, mk, en, rho);

            int[] mejor = null;
            double mejorCosto = double.MaxValue;
            if (Simulador.EsFactible(esc, en)) { mejor = (int[])x.Clone(); mejorCosto = costo0; }

            var movibles = new List<int>();
            for (int j = 0; j < n; j++) if (esc.Validas(j).Length > 1) movibles.Add(j);

            double T = 0.05 * costo0;
            double Tf = 1e-4 * costo0;
            double factor = Math.Pow(Tf / T, 1.0 / Math.Max(1, iteraciones));
            int aceptadas = 0;

            for (int it = 0; it < iteraciones && movibles.Count > 0; it++)
            {
                // Generar vecino (guardando lo necesario para deshacer)
                int a = -1, b = -1, viejaA = 0, viejaB = 0;
                if (rng.SiguienteDouble() < 0.5)
                {
                    for (int intento = 0; intento < 10; intento++)
                    {
                        int c = rng.Siguiente(n), d = rng.Siguiente(n);
                        if (x[c] != x[d] && esc.PuedeHacer(c, x[d]) && esc.PuedeHacer(d, x[c]))
                        {
                            a = c; b = d;
                            viejaA = x[a]; viejaB = x[b];
                            x[a] = viejaB; x[b] = viejaA;
                            break;
                        }
                    }
                }
                if (a < 0)
                {
                    a = movibles[rng.Siguiente(movibles.Count)];
                    viejaA = x[a];
                    int[] v = esc.Validas(a);
                    int k = v[rng.Siguiente(v.Length - 1)];
                    if (k == viejaA) k = v[v.Length - 1]; // elige entre las otras máquinas
                    x[a] = k;
                }

                Simulador.Evaluar(esc, x, out mk, out en);
                double fn = Objetivo(esc, mk, en, rho);
                double delta = fn - f;
                if (delta <= 0 || rng.SiguienteDouble() < Math.Exp(-delta / T))
                {
                    f = fn;
                    aceptadas++;
                    double c = esc.Costo(mk, en);
                    if (Simulador.EsFactible(esc, en) && c < mejorCosto - 1e-12)
                    {
                        mejorCosto = c;
                        mejor = (int[])x.Clone();
                    }
                }
                else
                {
                    x[a] = viejaA;
                    if (b >= 0) x[b] = viejaB;
                }
                T *= factor;
            }
            sw.Stop();

            string notas = iteraciones + " iteraciones, " + aceptadas + " aceptadas.";
            if (mejor == null)
            {
                mejor = x;
                notas += " No se encontró solución factible.";
            }
            var r = Simulador.Simular(esc, mejor, "Recocido Simulado");
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.notas = notas;
            return r;
        }

        private static double Objetivo(Escenario esc, double mk, double en, double rho)
        {
            return esc.Costo(mk, en) + rho * Math.Max(0, en - esc.energiaMax);
        }
    }
}
