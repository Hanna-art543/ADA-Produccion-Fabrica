using System;

namespace Fabrica.Core
{
    // Una tarea programada: qué máquina la hace y cuándo.
    [Serializable]
    public class Assignment
    {
        public int jobId;
        public int piezaId;
        public Etapa etapa;
        public int maquinaId;
        public double inicio;
        public double fin;
        public double energia;
    }
}
