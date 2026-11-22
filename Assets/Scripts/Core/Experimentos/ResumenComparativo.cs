using System;
using System.Collections.Generic;

namespace Fabrica.Core
{
    // Clases serializables para resumen_comparativo.json (compatibles con JsonUtility).
    [Serializable]
    public class FilaComparativa
    {
        public string algoritmo;
        public double makespan;
        public double energia;
        public double costo;
        public double gapPorcentaje;
        public double tiempoMs;
        public bool factible;
    }

    [Serializable]
    public class ResumenEscenario
    {
        public int numero;
        public string nombre;
        public int piezas;
        public int tareas;
        public int maquinas;
        public double energiaMax;
        public double alfa;
        public double beta;
        public string tipoReferencia; // "optimo B&B" o "cota inferior"
        public double costoReferencia;
        public double cotaInferior;
        public List<FilaComparativa> filas = new List<FilaComparativa>();
    }

    [Serializable]
    public class ResumenComparativo
    {
        public int semilla;
        public string nota;
        public List<ResumenEscenario> escenarios = new List<ResumenEscenario>();
    }
}
