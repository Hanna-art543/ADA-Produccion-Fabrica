using System;

namespace Fabrica.Core
{
    // Calibra λ (peso de la energía en el criterio goloso) con búsqueda binaria para que
    // la energía total quede lo más cerca posible de E_max sin pasarse.
    //  - λ = 0 => solo importa el tiempo (máxima energía). Si ya es factible, se usa.
    //  - λ muy grande => cada tarea va a su máquina de mínima energía (siempre factible si E_min ≤ E_max).
    //  - Se busca en escala logarítmica λ = 10^x, x ∈ [-4, 9], manteniendo el extremo alto factible.
    public static class CalibracionLambda
    {
        public const int IteracionesPorDefecto = 30;
        private const double ExpMin = -4, ExpMax = 9;

        public class Resultado
        {
            public double lambda;
            public int[] asignacion;
            public double energia;
            public int evaluaciones;
        }

        // 'asignar' construye una asignación para un λ dado (Goloso o Divide y Vencerás).
        public static Resultado Calibrar(Escenario esc, Func<double, int[]> asignar, int iteraciones)
        {
            var mejor = new Resultado { energia = double.MinValue };
            int evals = 0;

            // Evalúa un λ y guarda el factible con mayor energía (el más cercano a E_max)
            Func<double, bool> probar = lam =>
            {
                int[] a = asignar(lam);
                evals++;
                double e = esc.EnergiaDe(a);
                bool factible = Simulador.EsFactible(esc, e);
                if (factible && e > mejor.energia)
                {
                    mejor.lambda = lam;
                    mejor.asignacion = a;
                    mejor.energia = e;
                }
                return factible;
            };

            if (!probar(0))
            {
                double lo = ExpMin, hi = ExpMax;
                if (probar(Math.Pow(10, hi)))
                {
                    if (probar(Math.Pow(10, lo))) hi = lo;
                    for (int it = 0; it < iteraciones && hi > lo; it++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if (probar(Math.Pow(10, mid))) hi = mid; // factible: probar menos peso a la energía
                        else lo = mid;
                    }
                }
            }

            if (mejor.asignacion == null)
            {
                // Respaldo (no debería ocurrir): mínima energía por tarea
                mejor.asignacion = new int[esc.NumTareas];
                for (int j = 0; j < esc.NumTareas; j++) mejor.asignacion[j] = Goloso.MaquinaMinEnergia(esc, j);
                mejor.energia = esc.EnergiaDe(mejor.asignacion);
                mejor.lambda = double.PositiveInfinity;
            }
            mejor.evaluaciones = evals;
            return mejor;
        }
    }
}
