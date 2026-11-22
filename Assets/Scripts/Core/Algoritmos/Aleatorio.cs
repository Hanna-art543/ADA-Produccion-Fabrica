using System;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Resultado del aleatorio: el mejor plan + estadísticas de todas las corridas.
    [Serializable]
    public class PlanResultAleatorio : PlanResult
    {
        public int corridas;
        public int corridasFactibles;
        public double makespanPromedio;
        public double energiaPromedio;
        public double costoPromedio;
    }

    // Línea base: cada tarea a una máquina válida al azar; se repite N veces con semilla fija.
    public static class Aleatorio
    {
        public static PlanResultAleatorio Resolver(Escenario esc, int corridas, ulong semilla)
        {
            var sw = Stopwatch.StartNew();
            var rng = new Rng(semilla);
            int n = esc.NumTareas;
            int[] maq = new int[n];
            int[] mejor = null;
            bool mejorFactible = false;
            double mejorCosto = double.MaxValue;
            double sumaMk = 0, sumaE = 0, sumaC = 0;
            int factibles = 0;

            for (int c = 0; c < corridas; c++)
            {
                for (int j = 0; j < n; j++)
                {
                    int[] v = esc.Validas(j);
                    maq[j] = v[rng.Siguiente(v.Length)];
                }
                Simulador.Evaluar(esc, maq, out double mk, out double en);
                double costo = esc.Costo(mk, en);
                bool fact = Simulador.EsFactible(esc, en);
                sumaMk += mk; sumaE += en; sumaC += costo;
                if (fact) factibles++;

                // Mejor = menor costo entre las factibles (si no hay ninguna, menor costo)
                bool reemplazar = mejor == null
                                  || (fact && !mejorFactible)
                                  || (fact == mejorFactible && costo < mejorCosto);
                if (reemplazar)
                {
                    mejor = (int[])maq.Clone();
                    mejorFactible = fact;
                    mejorCosto = costo;
                }
            }
            sw.Stop();

            PlanResult basePlan = Simulador.Simular(esc, mejor, "Aleatorio (mejor)");
            var r = new PlanResultAleatorio
            {
                algoritmo = basePlan.algoritmo,
                escenario = basePlan.escenario,
                asignaciones = basePlan.asignaciones,
                makespan = basePlan.makespan,
                energiaTotal = basePlan.energiaTotal,
                energiaMax = basePlan.energiaMax,
                costoTotal = basePlan.costoTotal,
                factible = basePlan.factible,
                tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds,
                corridas = corridas,
                corridasFactibles = factibles,
                makespanPromedio = sumaMk / corridas,
                energiaPromedio = sumaE / corridas,
                costoPromedio = sumaC / corridas,
                notas = corridas + " corridas, " + factibles + " factibles (energía ≤ E_max)."
            };
            r.FijarMaquinas(mejor);
            return r;
        }
    }
}
