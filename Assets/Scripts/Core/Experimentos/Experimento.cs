using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fabrica.Core
{
    [Serializable]
    public class ConfigExperimento
    {
        public int semilla = 2024;
        public double alfa = 1.0;
        public double beta = 0.1;
        public double lambda = -1;            // λ inicial para B&B (< 0 => β/α); Goloso y D&V lo calibran
        public int iteracionesCalibracion = 30; // búsqueda binaria de λ (Goloso y D&V)
        public double holguraEnergia = 0.35;  // E_max = Emin + holgura·(Emax_posible − Emin)
        public int corridasAleatorio = 1000;
        public int iteracionesRecocido = 20000;
        public int resolucionPD = 4000;       // unidades discretas de energía en la PD
        public int maxPiezasBranchAndBound = 8;
        public int lambdasIntegrado = 12;     // valores de λ por etapa en el Integrado 2
    }

    public class EntradaPlan
    {
        public string clave; // sufijo del archivo: greedy, dc, pd, int1, int2, random, sa, bb
        public PlanResult plan;
    }

    public class ResultadoEscenario
    {
        public Escenario escenario;
        public List<EntradaPlan> planes = new List<EntradaPlan>();
        public bool referenciaEsOptimo;
        public double costoReferencia;
        public double cotaInferior;
    }

    // Corre todos los algoritmos en los 3 escenarios.
    public static class Experimento
    {
        public static List<ResultadoEscenario> Ejecutar(ConfigExperimento cfg)
        {
            double lambda = cfg.lambda >= 0 ? cfg.lambda : cfg.beta / cfg.alfa;
            var escenarios = GeneradorEscenarios.GenerarTodos(cfg.semilla, cfg.alfa, cfg.beta, lambda, cfg.holguraEnergia);
            var resultados = new List<ResultadoEscenario>();

            foreach (var esc in escenarios)
            {
                var res = new ResultadoEscenario { escenario = esc };
                ulong semillaEsc = (ulong)cfg.semilla * 7919UL + (ulong)esc.numero;

                // --- Métodos por separado ---
                PlanResult greedy = Goloso.Resolver(esc, cfg.iteracionesCalibracion);
                res.planes.Add(new EntradaPlan { clave = "greedy", plan = greedy });
                res.planes.Add(new EntradaPlan { clave = "dc", plan = DivideVenceras.Resolver(esc, cfg.iteracionesCalibracion) });
                res.planes.Add(new EntradaPlan { clave = "pd", plan = ProgramacionDinamica.Resolver(esc, cfg.resolucionPD) });

                // --- Métodos integrados (DyV + Goloso + PD) ---
                res.planes.Add(new EntradaPlan
                {
                    clave = "int1",
                    plan = PlanificadorIntegrado.Resolver(esc, cfg.iteracionesCalibracion, cfg.resolucionPD)
                });
                res.planes.Add(new EntradaPlan
                {
                    clave = "int2",
                    plan = PlanificadorIntegrado.ResolverB(esc, cfg.resolucionPD, cfg.lambdasIntegrado)
                });

                // --- Línea base y extras ---
                res.planes.Add(new EntradaPlan { clave = "random", plan = Aleatorio.Resolver(esc, cfg.corridasAleatorio, semillaEsc) });
                res.planes.Add(new EntradaPlan
                {
                    clave = "sa",
                    plan = RecocidoSimulado.Resolver(esc, greedy.MaquinaPorTarea(), cfg.iteracionesRecocido, semillaEsc + 1)
                });

                res.cotaInferior = Cotas.CotaCosto(esc);
                if (esc.numPiezas <= cfg.maxPiezasBranchAndBound)
                {
                    PlanResult bb = BranchAndBound.Resolver(esc, greedy.MaquinaPorTarea());
                    res.planes.Add(new EntradaPlan { clave = "bb", plan = bb });
                    res.referenciaEsOptimo = true;
                    res.costoReferencia = bb.costoTotal;
                }
                else
                {
                    res.referenciaEsOptimo = false;
                    res.costoReferencia = res.cotaInferior;
                }
                resultados.Add(res);
            }
            return resultados;
        }

        public static ResumenComparativo Resumir(List<ResultadoEscenario> resultados, int semilla)
        {
            var resumen = new ResumenComparativo
            {
                semilla = semilla,
                nota = "Gap = (costo - referencia) / referencia * 100. Referencia: óptimo de Branch & Bound "
                + "en el escenario pequeño; cota inferior (no alcanzable en general) en los demás."
            };
            foreach (var res in resultados)
            {
                var esc = res.escenario;
                var r = new ResumenEscenario
                {
                    numero = esc.numero,
                    nombre = esc.nombre,
                    piezas = esc.numPiezas,
                    tareas = esc.NumTareas,
                    maquinas = esc.NumMaquinas,
                    energiaMax = esc.energiaMax,
                    alfa = esc.alfa,
                    beta = esc.beta,
                    tipoReferencia = res.referenciaEsOptimo ? "optimo B&B" : "cota inferior",
                    costoReferencia = res.costoReferencia,
                    cotaInferior = res.cotaInferior
                };
                foreach (var e in res.planes)
                {
                    var p = e.plan;
                    var pa = p as PlanResultAleatorio;
                    if (pa == null)
                    {
                        r.filas.Add(Fila(p.algoritmo, p.makespan, p.energiaTotal, p.costoTotal, p.tiempoEjecucionMs,
                                         p.factible, res.costoReferencia));
                        continue;
                    }

                    // Aleatorio: mejor factible + promedio de factibles, o "sin solución factible"
                    r.notas.Add(string.Format(CultureInfo.InvariantCulture,
                                              "Aleatorio: {0:F1} % factibles ({1} de {2} corridas).",
                                              pa.porcentajeFactibles, pa.corridasFactibles, pa.corridas));
                    if (pa.hayFactible)
                    {
                        r.filas.Add(Fila(p.algoritmo, p.makespan, p.energiaTotal, p.costoTotal, p.tiempoEjecucionMs,
                                         true, res.costoReferencia));
                        r.filas.Add(Fila("Aleatorio (prom. factibles)", pa.makespanPromedioFactibles,
                                         pa.energiaPromedioFactibles, pa.costoPromedioFactibles,
                                         pa.tiempoEjecucionMs, true, res.costoReferencia));
                    }
                    else
                    {
                        r.filas.Add(new FilaComparativa
                        {
                            algoritmo = "Aleatorio",
                            sinSolucion = true,
                            tiempoMs = pa.tiempoEjecucionMs
                        });
                    }
                }
                resumen.escenarios.Add(r);
            }
            return resumen;
        }

        private static FilaComparativa Fila(string alg, double mk, double en, double costo, double ms, bool fact, double referencia)
        {
            return new FilaComparativa
            {
                algoritmo = alg,
                makespan = mk,
                energia = en,
                costo = costo,
                gapPorcentaje = (costo - referencia) / referencia * 100.0,
                tiempoMs = ms,
                factible = fact
            };
        }
    }
}
