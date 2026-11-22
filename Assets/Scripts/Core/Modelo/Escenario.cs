using System;
using System.Collections.Generic;

namespace Fabrica.Core
{
    // Instancia del problema: piezas (3 tareas c/u), máquinas, pesos del costo y límite de energía.
    [Serializable]
    public class Escenario
    {
        public int numero;
        public string nombre;
        public int numPiezas;
        public List<Job> jobs = new List<Job>();
        public List<Machine> maquinas = new List<Machine>();
        public double alfa = 1.0;   // peso del makespan
        public double beta = 0.1;   // peso de la energía
        public double lambda = 0.1; // peso de la energía en el criterio goloso
        public double energiaMax;   // E_max

        // Cachés (privadas, no se serializan)
        private double[] tiempo;   // p_ij en [j * M + i]
        private double[] energia;  // e_ij en [j * M + i]
        private int[][] validas;   // máquinas capaces por tarea
        private double[] tiempoMin;
        private double[] energiaMin;
        private int[] ordenPiezas; // piezas por trabajo total descendente (LPT)
        private int[] rangoPieza;
        private int[] ordenTareas; // tareas pieza por pieza, en orden LPT

        public int NumTareas { get { return jobs.Count; } }
        public int NumMaquinas { get { return maquinas.Count; } }

        public static int IdTarea(int pieza, int etapa)
        {
            return pieza * Etapas.Cantidad + etapa;
        }

        // Costo total = α·makespan + β·energía
        public double Costo(double makespan, double energiaTotal)
        {
            return alfa * makespan + beta * energiaTotal;
        }

        public void Preparar()
        {
            int n = jobs.Count, m = maquinas.Count;
            for (int j = 0; j < n; j++)
            {
                Job job = jobs[j];
                if (job.id != j || job.id != IdTarea(job.piezaId, (int)job.etapa))
                    throw new InvalidOperationException("Los jobs deben estar indexados como piezaId*3+etapa.");
            }

            tiempo = new double[n * m];
            energia = new double[n * m];
            validas = new int[n][];
            tiempoMin = new double[n];
            energiaMin = new double[n];
            for (int j = 0; j < n; j++)
            {
                var lista = new List<int>();
                tiempoMin[j] = double.MaxValue;
                energiaMin[j] = double.MaxValue;
                for (int i = 0; i < m; i++)
                {
                    Machine maq = maquinas[i];
                    double p = jobs[j].trabajo / maq.velocidad; // p_ij = w_j / v_i
                    tiempo[j * m + i] = p;
                    energia[j * m + i] = p * maq.potencia;      // e_ij = p_ij · P_i
                    if (maq.PuedeHacer(jobs[j].etapa))
                    {
                        lista.Add(i);
                        tiempoMin[j] = Math.Min(tiempoMin[j], p);
                        energiaMin[j] = Math.Min(energiaMin[j], p * maq.potencia);
                    }
                }
                if (lista.Count == 0)
                    throw new InvalidOperationException("La tarea " + j + " no tiene máquina capaz.");
                validas[j] = lista.ToArray();
            }

            // Orden LPT de piezas (desempate por id para ser determinista)
            var trabajoPieza = new double[numPiezas];
            for (int j = 0; j < n; j++) trabajoPieza[jobs[j].piezaId] += jobs[j].trabajo;
            ordenPiezas = new int[numPiezas];
            for (int p = 0; p < numPiezas; p++) ordenPiezas[p] = p;
            Array.Sort(ordenPiezas, (a, b) =>
            {
                int c = trabajoPieza[b].CompareTo(trabajoPieza[a]);
                return c != 0 ? c : a.CompareTo(b);
            });
            rangoPieza = new int[numPiezas];
            for (int r = 0; r < numPiezas; r++) rangoPieza[ordenPiezas[r]] = r;

            ordenTareas = new int[n];
            int k = 0;
            for (int r = 0; r < numPiezas; r++)
                for (int s = 0; s < Etapas.Cantidad; s++)
                    ordenTareas[k++] = IdTarea(ordenPiezas[r], s);
        }

        private void Asegurar()
        {
            if (tiempo == null) Preparar();
        }

        public double Tiempo(int j, int i) { Asegurar(); return tiempo[j * maquinas.Count + i]; }
        public double Energia(int j, int i) { Asegurar(); return energia[j * maquinas.Count + i]; }
        public int[] Validas(int j) { Asegurar(); return validas[j]; }
        public double TiempoMinimo(int j) { Asegurar(); return tiempoMin[j]; }
        public double EnergiaMinima(int j) { Asegurar(); return energiaMin[j]; }
        public int[] OrdenPiezas() { Asegurar(); return ordenPiezas; }
        public int RangoPieza(int p) { Asegurar(); return rangoPieza[p]; }
        public int[] OrdenTareas() { Asegurar(); return ordenTareas; }

        public bool PuedeHacer(int j, int i)
        {
            return maquinas[i].PuedeHacer(jobs[j].etapa);
        }

        // Energía mínima posible (cada tarea en su máquina más eficiente)
        public double EnergiaMinimaTotal()
        {
            double s = 0;
            for (int j = 0; j < NumTareas; j++) s += EnergiaMinima(j);
            return s;
        }

        public double EnergiaDe(int[] maq)
        {
            double s = 0;
            for (int j = 0; j < NumTareas; j++) s += Energia(j, maq[j]);
            return s;
        }
    }
}
