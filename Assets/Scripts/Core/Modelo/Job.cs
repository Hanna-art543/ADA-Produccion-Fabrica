using System;

namespace Fabrica.Core
{
    // Tarea = (pieza, etapa). Su id global es piezaId * 3 + etapa.
    [Serializable]
    public class Job
    {
        public int id;
        public int piezaId;
        public Etapa etapa;
        public double trabajo; // unidades de trabajo

        public Job() { }

        public Job(int id, int piezaId, Etapa etapa, double trabajo)
        {
            this.id = id;
            this.piezaId = piezaId;
            this.etapa = etapa;
            this.trabajo = trabajo;
        }
    }
}
