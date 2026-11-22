using System;
using System.Collections.Generic;

namespace Fabrica.Core
{
    // Convierte una asignación (máquina por tarea) en una línea de tiempo.
    // Regla única para todos los algoritmos (lista dinámica):
    //   en cada paso se programa, entre las tareas listas (siguiente etapa de cada pieza),
    //   la que puede empezar antes; inicio = max(fin etapa anterior de la pieza, máquina libre).
    //   Empates: pieza con más trabajo primero (orden LPT).
    // Así todos los algoritmos se comparan en el mismo espacio de soluciones.
    public static class Simulador
    {
        public const double Tolerancia = 1e-6;

        public static PlanResult Simular(Escenario esc, int[] maq, string algoritmo)
        {
            var lista = new List<Assignment>(esc.NumTareas);
            bool valido = Ejecutar(esc, maq, lista, out double mk, out double en);
            var r = new PlanResult
            {
                algoritmo = algoritmo,
                escenario = esc.nombre,
                asignaciones = lista,
                makespan = mk,
                energiaTotal = en,
                energiaMax = esc.energiaMax,
                costoTotal = esc.Costo(mk, en),
                factible = valido && en <= esc.energiaMax + Tolerancia
            };
            if (!valido) r.notas = "Asignación inválida: una máquina no puede hacer la etapa asignada.";
            r.FijarMaquinas(maq);
            return r;
        }

        // Versión rápida (sin lista de asignaciones) para búsquedas que simulan muchas veces.
        public static bool Evaluar(Escenario esc, int[] maq, out double makespan, out double energia)
        {
            return Ejecutar(esc, maq, null, out makespan, out energia);
        }

        public static bool EsFactible(Escenario esc, double energia)
        {
            return energia <= esc.energiaMax + Tolerancia;
        }

        // Devuelve false si alguna máquina no puede hacer la etapa de su tarea.
        private static bool Ejecutar(Escenario esc, int[] maq, List<Assignment> salida,
                                     out double makespan, out double energia)
        {
            int P = esc.numPiezas, M = esc.NumMaquinas, n = esc.NumTareas;
            if (maq == null || maq.Length != n) throw new ArgumentException("La asignación debe tener una máquina por tarea.");

            int[] orden = esc.OrdenPiezas();
            int[] siguiente = new int[P];   // próxima etapa pendiente de cada pieza
            double[] listo = new double[P]; // fin de la etapa anterior de cada pieza
            double[] libre = new double[M]; // instante en que cada máquina queda libre
            makespan = 0;
            energia = 0;
            bool valido = true;

            for (int paso = 0; paso < n; paso++)
            {
                int mejorP = -1;
                double mejorInicio = double.MaxValue;
                for (int r = 0; r < P; r++)
                {
                    int p = orden[r];
                    if (siguiente[p] >= Etapas.Cantidad) continue;
                    int i = maq[Escenario.IdTarea(p, siguiente[p])];
                    if (i < 0 || i >= M) throw new ArgumentException("Índice de máquina fuera de rango: " + i);
                    double s = Math.Max(listo[p], libre[i]);
                    if (s < mejorInicio - 1e-12)
                    {
                        mejorInicio = s;
                        mejorP = p;
                    }
                }

                int j = Escenario.IdTarea(mejorP, siguiente[mejorP]);
                int m = maq[j];
                if (!esc.PuedeHacer(j, m)) valido = false;
                double fin = mejorInicio + esc.Tiempo(j, m);
                double e = esc.Energia(j, m);
                listo[mejorP] = fin;
                libre[m] = fin;
                siguiente[mejorP]++;
                energia += e;
                if (fin > makespan) makespan = fin;

                if (salida != null)
                {
                    salida.Add(new Assignment
                    {
                        jobId = j,
                        piezaId = mejorP,
                        etapa = esc.jobs[j].etapa,
                        maquinaId = esc.maquinas[m].id,
                        inicio = mejorInicio,
                        fin = fin,
                        energia = e
                    });
                }
            }
            return valido;
        }
    }
}
