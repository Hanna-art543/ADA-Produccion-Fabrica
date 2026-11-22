using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fabrica.Core;

namespace Fabrica.ConsoleTest
{
    // Corre el experimento completo fuera de Unity, exporta los JSON y verifica resultados.
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var cfg = new ConfigExperimento();

            var resultados = Experimento.Ejecutar(cfg);
            var resumen = Experimento.Resumir(resultados, cfg.semilla);
            Console.WriteLine(Comparador.Tabla(resumen));

            // JSON con campos públicos (equivalente a JsonUtility)
            var opciones = new JsonSerializerOptions
            {
                IncludeFields = true,
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            string carpeta = args.Length > 0 ? args[0] : Path.Combine(BuscarRaiz(), "Resultados");
            var archivos = ExportadorJson.Exportar(resultados, resumen, carpeta,
                                                   o => JsonSerializer.Serialize(o, o.GetType(), opciones));
            Console.WriteLine("Exportados " + archivos.Count + " archivos a " + Path.GetFullPath(carpeta));
            Console.WriteLine();

            return Verificar(resultados, resumen) ? 0 : 1;
        }

        private static bool Verificar(List<ResultadoEscenario> resultados, ResumenComparativo resumen)
        {
            Console.WriteLine("=== Verificaciones ===");
            bool ok = true;

            foreach (var res in resultados)
            {
                var esc = res.escenario;
                foreach (var e in res.planes)
                {
                    var p = e.plan;
                    // Recalcular con el Simulador y comparar
                    Simulador.Evaluar(esc, p.MaquinaPorTarea(), out double mk, out double en);
                    ok &= Check(Math.Abs(mk - p.makespan) < 1e-6 && Math.Abs(en - p.energiaTotal) < 1e-6,
                                esc.nombre + " / " + p.algoritmo + ": el plan coincide con Simular()");
                    ok &= Check(PrecedenciasOk(esc, p), esc.nombre + " / " + p.algoritmo + ": precedencias y máquinas sin solapes");
                }

                PlanResult pd = Buscar(res, "pd");
                ok &= Check(pd.energiaTotal <= esc.energiaMax + 1e-9,
                            string.Format("{0} / PD: energía {1:F2} ≤ E_max {2:F2}", esc.nombre, pd.energiaTotal, esc.energiaMax));

                if (res.referenciaEsOptimo)
                {
                    PlanResult bb = Buscar(res, "bb");
                    double gapBB = (bb.costoTotal - res.costoReferencia) / res.costoReferencia * 100;
                    ok &= Check(Math.Abs(gapBB) < 1e-9, esc.nombre + " / B&B: gap = " + gapBB.ToString("F2") + " %");

                    double fb = FuerzaBruta(esc, out long combinaciones);
                    ok &= Check(Math.Abs(fb - bb.costoTotal) < 1e-6,
                                string.Format("{0} / B&B = fuerza bruta ({1} combinaciones): {2:F4} vs {3:F4}",
                                              esc.nombre, combinaciones, bb.costoTotal, fb));

                    foreach (var e in res.planes)
                        if (e.plan.factible)
                            ok &= Check(e.plan.costoTotal >= bb.costoTotal - 1e-6,
                                        esc.nombre + " / " + e.plan.algoritmo + ": costo ≥ óptimo");
                }
                else
                {
                    foreach (var e in res.planes)
                        ok &= Check(e.plan.costoTotal >= res.cotaInferior - 1e-6,
                                    esc.nombre + " / " + e.plan.algoritmo + ": costo ≥ cota inferior");
                }
            }

            Console.WriteLine(ok ? "TODAS LAS VERIFICACIONES OK" : "HAY VERIFICACIONES FALLIDAS");
            return ok;
        }

        private static PlanResult Buscar(ResultadoEscenario res, string clave)
        {
            foreach (var e in res.planes) if (e.clave == clave) return e.plan;
            throw new InvalidOperationException("No existe el plan " + clave);
        }

        // Cada pieza respeta el orden de etapas y ninguna máquina hace dos tareas a la vez.
        private static bool PrecedenciasOk(Escenario esc, PlanResult p)
        {
            var porJob = new Assignment[esc.NumTareas];
            foreach (var a in p.asignaciones) porJob[a.jobId] = a;
            for (int pz = 0; pz < esc.numPiezas; pz++)
                for (int s = 1; s < Etapas.Cantidad; s++)
                    if (porJob[Escenario.IdTarea(pz, s)].inicio < porJob[Escenario.IdTarea(pz, s - 1)].fin - 1e-9) return false;

            var lista = new List<Assignment>(p.asignaciones);
            lista.Sort((a, b) => a.maquinaId != b.maquinaId ? a.maquinaId.CompareTo(b.maquinaId) : a.inicio.CompareTo(b.inicio));
            for (int i = 1; i < lista.Count; i++)
                if (lista[i].maquinaId == lista[i - 1].maquinaId && lista[i].inicio < lista[i - 1].fin - 1e-9) return false;
            foreach (var a in p.asignaciones)
                if (!esc.PuedeHacer(a.jobId, a.maquinaId)) return false;
            return true;
        }

        // Enumera todas las asignaciones (solo para el escenario pequeño) y devuelve el costo mínimo factible.
        private static double FuerzaBruta(Escenario esc, out long combinaciones)
        {
            int n = esc.NumTareas;
            int[] idx = new int[n];
            int[] maq = new int[n];
            double mejor = double.MaxValue;
            combinaciones = 0;
            while (true)
            {
                for (int j = 0; j < n; j++) maq[j] = esc.Validas(j)[idx[j]];
                Simulador.Evaluar(esc, maq, out double mk, out double en);
                combinaciones++;
                if (Simulador.EsFactible(esc, en)) mejor = Math.Min(mejor, esc.Costo(mk, en));

                int k = 0;
                while (k < n && ++idx[k] == esc.Validas(k).Length) { idx[k] = 0; k++; }
                if (k == n) break;
            }
            return mejor;
        }

        private static bool Check(bool cond, string msg)
        {
            Console.WriteLine((cond ? "[OK]    " : "[FALLO] ") + msg);
            return cond;
        }

        // Sube desde el directorio actual hasta encontrar la carpeta que contiene Assets/
        private static string BuscarRaiz()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))) return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }
    }
}
