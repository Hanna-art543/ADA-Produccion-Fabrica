namespace Fabrica.Core
{
    // Etapas del proceso. Cada pieza pasa por las tres, en este orden.
    public enum Etapa
    {
        Corte = 0,
        Ensamblaje = 1,
        Empaquetado = 2
    }

    public static class Etapas
    {
        public const int Cantidad = 3;
    }
}
