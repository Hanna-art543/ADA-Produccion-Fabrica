using System;
using System.Collections.Generic;

namespace Fabrica.Core
{
    [Serializable]
    public class Machine
    {
        public int id;
        public double velocidad; // unidades de trabajo por unidad de tiempo
        public double potencia;  // energía por unidad de tiempo
        public List<Etapa> etapas = new List<Etapa>(); // etapas que puede hacer

        public Machine() { }

        public Machine(int id, double velocidad, double potencia, List<Etapa> etapas)
        {
            this.id = id;
            this.velocidad = velocidad;
            this.potencia = potencia;
            this.etapas = etapas;
        }

        public bool PuedeHacer(Etapa e)
        {
            return etapas.Contains(e);
        }
    }
}
