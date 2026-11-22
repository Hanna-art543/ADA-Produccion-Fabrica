using System;

namespace Fabrica.Core
{
    // Si una asignación excede E_max, mueve tareas a máquinas más eficientes
    // eligiendo siempre el mayor ahorro de energía por unidad de tiempo añadida.
    public static class ReparacionEnergia
    {
        public static int Reparar(Escenario esc, int[] maq)
        {
            double energia = esc.EnergiaDe(maq);
            int movimientos = 0;
            while (energia > esc.energiaMax + Simulador.Tolerancia)
            {
                int mejorJ = -1, mejorK = -1;
                double mejorRazon = double.MinValue, mejorAhorro = 0;
                for (int j = 0; j < esc.NumTareas; j++)
                {
                    int actual = maq[j];
                    foreach (int k in esc.Validas(j))
                    {
                        double ahorro = esc.Energia(j, actual) - esc.Energia(j, k);
                        if (ahorro <= 0) continue;
                        double dt = esc.Tiempo(j, k) - esc.Tiempo(j, actual);
                        double razon = ahorro / Math.Max(dt, 1e-6);
                        if (razon > mejorRazon)
                        {
                            mejorRazon = razon;
                            mejorJ = j;
                            mejorK = k;
                            mejorAhorro = ahorro;
                        }
                    }
                }
                if (mejorJ < 0) break; // ya está todo en la máquina de mínima energía
                maq[mejorJ] = mejorK;
                energia -= mejorAhorro;
                movimientos++;
            }
            return movimientos;
        }
    }
}
