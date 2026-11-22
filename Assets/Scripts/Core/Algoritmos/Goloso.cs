using System;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Goloso LPT: piezas por trabajo total descendente; cada etapa de la pieza va a
    // la máquina que minimiza (fin + λ·energía). Solo se aceptan máquinas que dejan
    // energía suficiente para el resto (cota de energía mínima restante) => siempre factible.
    public static class Goloso
    {
        public static PlanResult Resolver(Escenario esc)
        {
            var sw = Stopwatch.StartNew();
            int[] maq = Asignar(esc, esc.lambda);
            sw.Stop();
            var r = Simulador.Simular(esc, maq, "Goloso (LPT)");
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            return r;
        }

        public static int[] Asignar(Escenario esc, double lambda)
        {
            int n = esc.NumTareas, M = esc.NumMaquinas;
            int[] maq = new int[n];
            double[] libre = new double[M];
            double energiaUsada = 0;
            double energiaMinRestante = esc.EnergiaMinimaTotal();

            foreach (int p in esc.OrdenPiezas())
            {
                double listo = 0; // fin de la etapa anterior de esta pieza
                for (int s = 0; s < Etapas.Cantidad; s++)
                {
                    int j = Escenario.IdTarea(p, s);
                    energiaMinRestante -= esc.EnergiaMinima(j);

                    int mejor = -1;
                    double mejorCrit = double.MaxValue, mejorE = double.MaxValue, mejorFin = 0;
                    foreach (int i in esc.Validas(j))
                    {
                        double e = esc.Energia(j, i);
                        // ¿Sigue siendo posible terminar sin pasar E_max?
                        if (energiaUsada + e + energiaMinRestante > esc.energiaMax + Simulador.Tolerancia) continue;
                        double fin = Math.Max(listo, libre[i]) + esc.Tiempo(j, i);
                        double crit = fin + lambda * e;
                        if (crit < mejorCrit - 1e-12 || (Math.Abs(crit - mejorCrit) <= 1e-12 && e < mejorE))
                        {
                            mejor = i;
                            mejorCrit = crit;
                            mejorE = e;
                            mejorFin = fin;
                        }
                    }
                    if (mejor < 0) // solo si E_max < energía mínima (instancia infactible)
                    {
                        mejor = MaquinaMinEnergia(esc, j);
                        mejorE = esc.Energia(j, mejor);
                        mejorFin = Math.Max(listo, libre[mejor]) + esc.Tiempo(j, mejor);
                    }

                    maq[j] = mejor;
                    libre[mejor] = mejorFin;
                    listo = mejorFin;
                    energiaUsada += mejorE;
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
