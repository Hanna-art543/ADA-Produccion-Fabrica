using System;
using System.Collections.Generic;

namespace Fabrica.Core
{
    // Resultado de un algoritmo, ya secuenciado por el Simulador.
    [Serializable]
    public class PlanResult
    {
        public string algoritmo;
        public string escenario;
        public List<Assignment> asignaciones = new List<Assignment>();
        public double makespan;
        public double energiaTotal;
        public double energiaMax;
        public double costoTotal;
        public bool factible;
        public double tiempoEjecucionMs;
        public string notas = "";

        // Máquina elegida por tarea (no se serializa: es privada)
        private int[] maquinaPorTarea;

        public void FijarMaquinas(int[] maq)
        {
            maquinaPorTarea = (int[])maq.Clone();
        }

        public int[] MaquinaPorTarea()
        {
            return maquinaPorTarea == null ? null : (int[])maquinaPorTarea.Clone();
        }
    }
}
