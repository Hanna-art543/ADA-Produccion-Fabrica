using System;
using System.Diagnostics;
using System.Globalization;

namespace Fabrica.Core
{
    // Goloso LPT: piezas por trabajo total descendente; cada etapa de la pieza va a
    // la máquina que minimiza (fin + λ·energía).
    // λ se calibra con búsqueda binaria (CalibracionLambda) para usar la energía
    // disponible hasta E_max sin pasarse.
    public static class Goloso
    {
        public static PlanResult Resolver(Escenario esc, int iteracionesCalibracion)
        {
            var sw = Stopwatch.StartNew();
            var cal = CalibracionLambda.Calibrar(esc, lam => Asignar(esc, lam), iteracionesCalibracion);
            sw.Stop();
            var r = Simulador.Simular(esc, cal.asignacion, "Goloso (LPT)");
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.notas = NotaCalibracion(cal);
            return r;
        }

        public static string NotaCalibracion(CalibracionLambda.Resultado cal)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "λ calibrado = {0:G6} ({1} evaluaciones de la búsqueda binaria).", cal.lambda, cal.evaluaciones);
        }

        // Una pasada golosa con λ fijo. O(n·m) tras el orden LPT.
        public static int[] Asignar(Escenario esc, double lambda)
        {
            int n = esc.NumTareas, M = esc.NumMaquinas;
            int[] maq = new int[n];
            double[] libre = new double[M];

            foreach (int p in esc.OrdenPiezas())
            {
                double listo = 0; // fin de la etapa anterior de esta pieza
                for (int s = 0; s < Etapas.Cantidad; s++)
                {
                    int j = Escenario.IdTarea(p, s);
                    int mejor = -1;
                    double mejorCrit = double.MaxValue, mejorE = double.MaxValue, mejorFin = 0;
                    foreach (int i in esc.Validas(j))
                    {
                        double e = esc.Energia(j, i);
                        double fin = Math.Max(listo, libre[i]) + esc.Tiempo(j, i);
                        double crit = fin + lambda * e;
                        // Desempate por menor energía
                        if (crit < mejorCrit || (crit == mejorCrit && e < mejorE))
                        {
                            mejor = i;
                            mejorCrit = crit;
                            mejorE = e;
                            mejorFin = fin;
                        }
                    }
                    maq[j] = mejor;
                    libre[mejor] = mejorFin;
                    listo = mejorFin;
                }
            }
            return maq;
        }

        public static int MaquinaMinEnergia(Escenario esc, int j)
        {
            int mejor = -1;
            double mejorE = double.MaxValue;
            foreach (int i in esc.Validas(j))
            {
                if (esc.Energia(j, i) < mejorE)
                {
                    mejorE = esc.Energia(j, i);
                    mejor = i;
                }
            }
            return mejor;
        }
    }
}
