using System;
using System.Collections.Generic;

namespace Fabrica.Core
{
    // Genera los 3 escenarios de prueba con semilla fija.
    public static class GeneradorEscenarios
    {
        public static List<Escenario> GenerarTodos(int semilla, double alfa, double beta, double lambda, double holguraEnergia)
        {
            return new List<Escenario>
            {
                Generar(1, "Pequeño", 6, 3, semilla, alfa, beta, lambda, holguraEnergia),
                Generar(2, "Mediano", 20, 5, semilla, alfa, beta, lambda, holguraEnergia),
                Generar(3, "Grande", 50, 8, semilla, alfa, beta, lambda, holguraEnergia)
            };
        }

        public static Escenario Generar(int numero, string nombre, int piezas, int numMaquinas, int semilla,
                                        double alfa, double beta, double lambda, double holguraEnergia)
        {
            var rng = new Rng((ulong)semilla * 1000003UL + (ulong)numero);
            var esc = new Escenario
            {
                numero = numero,
                nombre = nombre,
                numPiezas = piezas,
                alfa = alfa,
                beta = beta,
                lambda = lambda
            };

            // Máquinas: las rápidas consumen más energía por unidad de trabajo (P ∝ v^1.6),
            // así hay un trade-off real entre tiempo y energía.
            for (int i = 0; i < numMaquinas; i++)
            {
                double v = Math.Round(1.0 + 2.0 * rng.SiguienteDouble(), 1);
                double pot = Math.Round(2.0 * Math.Pow(v, 1.6) * (0.85 + 0.3 * rng.SiguienteDouble()), 2);
                esc.maquinas.Add(new Machine(i, v, pot, EtapasDeMaquina(i, numMaquinas, rng)));
            }
            AsegurarDosMaquinasPorEtapa(esc, rng);

            // Piezas: trabajo entero en [10, 40] por etapa
            for (int p = 0; p < piezas; p++)
                for (int s = 0; s < Etapas.Cantidad; s++)
                    esc.jobs.Add(new Job(Escenario.IdTarea(p, s), p, (Etapa)s, rng.Siguiente(10, 41)));

            esc.Preparar();

            // E_max entre la energía mínima y la máxima posibles
            double eMin = 0, eMax = 0;
            for (int j = 0; j < esc.NumTareas; j++)
            {
                eMin += esc.EnergiaMinima(j);
                double peor = 0;
                foreach (int i in esc.Validas(j)) peor = Math.Max(peor, esc.Energia(j, i));
                eMax += peor;
            }
            esc.energiaMax = Math.Round(eMin + holguraEnergia * (eMax - eMin), 2);
            return esc;
        }

        private static List<Etapa> EtapasDeMaquina(int i, int numMaquinas, Rng rng)
        {
            // Escenario de 3 máquinas: cada una hace 2 etapas (cada etapa tiene 2 máquinas)
            if (numMaquinas == 3)
            {
                switch (i)
                {
                    case 0: return new List<Etapa> { Etapa.Corte, Etapa.Ensamblaje };
                    case 1: return new List<Etapa> { Etapa.Ensamblaje, Etapa.Empaquetado };
                    default: return new List<Etapa> { Etapa.Corte, Etapa.Empaquetado };
                }
            }
            // Resto: una etapa principal + otra al azar (30 %: las tres)
            var lista = new List<Etapa> { (Etapa)(i % 3) };
            if (rng.SiguienteDouble() < 0.3)
            {
                lista = new List<Etapa> { Etapa.Corte, Etapa.Ensamblaje, Etapa.Empaquetado };
            }
            else
            {
                int otra = (i % 3 + 1 + rng.Siguiente(2)) % 3;
                lista.Add((Etapa)otra);
                lista.Sort();
            }
            return lista;
        }

        private static void AsegurarDosMaquinasPorEtapa(Escenario esc, Rng rng)
        {
            for (int s = 0; s < Etapas.Cantidad; s++)
            {
                int cuenta = 0;
                foreach (var m in esc.maquinas) if (m.PuedeHacer((Etapa)s)) cuenta++;
                while (cuenta < 2)
                {
                    var m = esc.maquinas[rng.Siguiente(esc.maquinas.Count)];
                    if (m.PuedeHacer((Etapa)s)) continue;
                    m.etapas.Add((Etapa)s);
                    m.etapas.Sort();
                    cuenta++;
                }
            }
        }
    }
}
