using System;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Resultado del aleatorio: el mejor plan factible + estadísticas de las corridas factibles.
    // Si ninguna corrida es factible: hayFactible = false y el plan va vacío.
    [Serializable]
    public class PlanResultAleatorio : PlanResult
    {
        public bool hayFactible;
        public int corridas;
        public int corridasFactibles;
        public double porcentajeFactibles;
        public double makespanPromedioFactibles;
        public double energiaPromedioFactibles;
        public double costoPromedioFactibles;
    }

    // Línea base: cada tarea a una máquina válida al azar; se repite N veces con semilla fija.
    public static class Aleatorio
    {
        public const string Nombre = "Aleatorio (mejor factible)";

        public static PlanResultAleatorio Resolver(Escenario esc, int corridas, ulong semilla)
        {
            var sw = Stopwatch.StartNew();
            var rng = new Rng(semilla);
            int n = esc.NumTareas;
            int[] maq = new int[n];
            int[] mejor = null;
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
                if (!Simulador.EsFactible(esc, en)) continue;

                // Solo cuentan las corridas factibles
                double costo = esc.Costo(mk, en);
                factibles++;
                sumaMk += mk; sumaE += en; sumaC += costo;
                if (costo < mejorCosto)
                {
                    mejorCosto = costo;
                    mejor = (int[])maq.Clone();
                }
            }
            sw.Stop();

            PlanResultAleatorio r;
            if (mejor != null)
            {
                PlanResult p = Simulador.Simular(esc, mejor, Nombre);
                r = new PlanResultAleatorio
                {
                    asignaciones = p.asignaciones,
                    makespan = p.makespan,
                    energiaTotal = p.energiaTotal,
                    costoTotal = p.costoTotal,
                    factible = p.factible,
                    hayFactible = true,
                    makespanPromedioFactibles = sumaMk / factibles,
                    energiaPromedioFactibles = sumaE / factibles,
                    costoPromedioFactibles = sumaC / factibles
                };
                r.FijarMaquinas(mejor);
            }
            else
            {
                r = new PlanResultAleatorio { hayFactible = false, factible = false };
            }

            r.algoritmo = Nombre;
            r.escenario = esc.nombre;
            r.energiaMax = esc.energiaMax;
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.corridas = corridas;
            r.corridasFactibles = factibles;
            r.porcentajeFactibles = 100.0 * factibles / corridas;
            r.notas = factibles > 0
                ? factibles + " de " + corridas + " corridas factibles (energía ≤ E_max)."
                : "Sin solución factible: ninguna de las " + corridas + " corridas cumple E_max.";
            return r;
        }
    }
}
