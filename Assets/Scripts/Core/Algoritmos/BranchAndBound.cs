using System;
using System.Diagnostics;

namespace Fabrica.Core
{
    // Branch & Bound exacto sobre el espacio de asignaciones (máquina por tarea),
    // evaluando cada hoja con el mismo Simulador que el resto de algoritmos.
    // Cota inferior de un nodo (válida para cualquier completación):
    //   makespan ≥ max( carga de cada máquina,
    //                   cadena de cada pieza (etapas asignadas + tiempo mínimo de las pendientes),
    //                   (tiempo total asignado + tiempo mínimo restante) / M )
    //   energía  ≥ energía asignada + energía mínima restante
    // Se poda si α·LB_mk + β·LB_e ≥ mejor costo, o si LB_e > E_max.
    public static class BranchAndBound
    {
        public static PlanResult Resolver(Escenario esc, int[] solucionInicial)
        {
            var sw = Stopwatch.StartNew();
            var b = new Busqueda(esc);
            if (solucionInicial != null) b.ProbarInicial(solucionInicial);
            b.Ramificar(0);
            sw.Stop();

            int[] mejor = b.mejorMaq;
            string notas = "Nodos explorados: " + b.nodos + ", hojas evaluadas: " + b.hojas + ".";
            if (mejor == null)
            {
                mejor = new int[esc.NumTareas];
                for (int j = 0; j < mejor.Length; j++) mejor[j] = Goloso.MaquinaMinEnergia(esc, j);
                notas += " No se encontró solución factible.";
            }
            var r = Simulador.Simular(esc, mejor, "Branch & Bound");
            r.tiempoEjecucionMs = sw.Elapsed.TotalMilliseconds;
            r.notas = notas;
            return r;
        }

        private class Busqueda
        {
            private readonly Escenario esc;
            private readonly int n, M;
            private readonly int[] orden;
            private readonly int[] pieza;
            private readonly double[] minP;
            private readonly double[] restoMinP; // suma de tiempos mínimos desde la posición t
            private readonly double[] restoMinE; // suma de energías mínimas desde la posición t
            private readonly int[][] candidatos;

            private readonly int[] maq;
            private readonly double[] carga;
            private readonly double[] cadena;
            private double sumaP, energia;

            public double mejorCosto = double.MaxValue;
            public int[] mejorMaq;
            public long nodos, hojas;

            public Busqueda(Escenario esc)
            {
                this.esc = esc;
                n = esc.NumTareas;
                M = esc.NumMaquinas;
                orden = esc.OrdenTareas();
                pieza = new int[n];
                minP = new double[n];
                restoMinP = new double[n + 1];
                restoMinE = new double[n + 1];
                candidatos = new int[n][];
                cadena = new double[esc.numPiezas];

                for (int t = n - 1; t >= 0; t--)
                {
                    int j = orden[t];
                    pieza[t] = esc.jobs[j].piezaId;
                    minP[t] = esc.TiempoMinimo(j);
                    restoMinP[t] = restoMinP[t + 1] + minP[t];
                    restoMinE[t] = restoMinE[t + 1] + esc.EnergiaMinima(j);
                    cadena[pieza[t]] += minP[t];

                    // Probar primero las máquinas más prometedoras (tiempo + λ·energía)
                    var c = (int[])esc.Validas(j).Clone();
                    Array.Sort(c, (a, b2) => (esc.Tiempo(j, a) + esc.lambda * esc.Energia(j, a))
                                             .CompareTo(esc.Tiempo(j, b2) + esc.lambda * esc.Energia(j, b2)));
                    candidatos[t] = c;
                }
                maq = new int[n];
                carga = new double[M];
            }

            public void ProbarInicial(int[] sol)
            {
                Simulador.Evaluar(esc, sol, out double mk, out double en);
                if (Simulador.EsFactible(esc, en))
                {
                    mejorCosto = esc.Costo(mk, en);
                    mejorMaq = (int[])sol.Clone();
                }
            }

            public void Ramificar(int t)
            {
                nodos++;
                if (t == n)
                {
                    hojas++;
                    Simulador.Evaluar(esc, maq, out double mk, out double en);
                    double c = esc.Costo(mk, en);
                    if (Simulador.EsFactible(esc, en) && c < mejorCosto - 1e-9)
                    {
                        mejorCosto = c;
                        mejorMaq = (int[])maq.Clone();
                    }
                    return;
                }

                int j = orden[t];
                int p = pieza[t];
                foreach (int k in candidatos[t])
                {
                    double pt = esc.Tiempo(j, k), e = esc.Energia(j, k);
                    double lbE = energia + e + restoMinE[t + 1];
                    if (lbE > esc.energiaMax + Simulador.Tolerancia) continue; // no puede ser factible

                    // Aplicar
                    carga[k] += pt;
                    cadena[p] += pt - minP[t];
                    sumaP += pt;
                    energia += e;

                    double lbMk = (sumaP + restoMinP[t + 1]) / M;
                    for (int i = 0; i < M; i++) if (carga[i] > lbMk) lbMk = carga[i];
                    for (int q = 0; q < cadena.Length; q++) if (cadena[q] > lbMk) lbMk = cadena[q];
                    double lb = esc.Costo(lbMk, lbE);

                    if (lb < mejorCosto - 1e-9)
                    {
                        maq[j] = k;
                        Ramificar(t + 1);
                    }

                    // Deshacer
                    carga[k] -= pt;
                    cadena[p] -= pt - minP[t];
                    sumaP -= pt;
                    energia -= e;
                }
            }
        }
    }
}
