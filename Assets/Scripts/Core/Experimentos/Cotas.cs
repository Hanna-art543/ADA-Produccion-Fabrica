using System;

namespace Fabrica.Core
{
    // Cota inferior del costo, para escenarios donde no se calcula el óptimo.
    //   makespan ≥ max( Σw / Σv                                   (toda la fábrica),
    //                   Σw_etapa / Σv de las máquinas capaces       (por etapa),
    //                   Σ_etapas tiempo mínimo de la pieza          (cadena de cada pieza) )
    //   energía  ≥ Σ_j min_i e_ij
    //   costo    ≥ α·LB_makespan + β·LB_energía
    // Es una cota (no necesariamente alcanzable): el gap contra ella sobreestima el gap real.
    public static class Cotas
    {
        public static double CotaMakespan(Escenario esc)
        {
            double wTotal = 0, vTotal = 0;
            foreach (var m in esc.maquinas) vTotal += m.velocidad;
            double[] wEtapa = new double[Etapas.Cantidad];
            double[] cadena = new double[esc.numPiezas];
            foreach (var job in esc.jobs)
            {
                wTotal += job.trabajo;
                wEtapa[(int)job.etapa] += job.trabajo;
                cadena[job.piezaId] += esc.TiempoMinimo(job.id);
            }

            double lb = wTotal / vTotal;
            for (int s = 0; s < Etapas.Cantidad; s++)
            {
                double v = 0;
                foreach (var m in esc.maquinas) if (m.PuedeHacer((Etapa)s)) v += m.velocidad;
                lb = Math.Max(lb, wEtapa[s] / v);
            }
            foreach (double c in cadena) lb = Math.Max(lb, c);
            return lb;
        }

        public static double CotaCosto(Escenario esc)
        {
            return esc.Costo(CotaMakespan(esc), esc.EnergiaMinimaTotal());
        }
    }
}
