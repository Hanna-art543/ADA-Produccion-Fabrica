namespace Fabrica.Core
{
    // Generador pseudoaleatorio propio (SplitMix64) para obtener los mismos
    // resultados en Unity (Mono/IL2CPP) y en .NET, con la misma semilla.
    public class Rng
    {
        private ulong estado;

        public Rng(ulong semilla)
        {
            estado = semilla;
        }

        public ulong SiguienteU64()
        {
            estado += 0x9E3779B97F4A7C15UL;
            ulong z = estado;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Real en [0, 1)
        public double SiguienteDouble()
        {
            return (SiguienteU64() >> 11) * (1.0 / 9007199254740992.0);
        }

        // Entero en [0, max)
        public int Siguiente(int max)
        {
            return (int)(SiguienteU64() % (ulong)max);
        }

        // Entero en [min, max)
        public int Siguiente(int min, int max)
        {
            return min + Siguiente(max - min);
        }
    }
}
