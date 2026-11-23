using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Fabrica.Core;

namespace Fabrica.UI
{
    /// <summary>
    /// Cálculos puros de la UI (sin UnityEngine), para poder probarlos fuera de Unity:
    /// nombres de algoritmos, marcas del eje, % de mejora y lectura del resumen.
    /// </summary>
    public static class CalculosFabrica
    {
        public static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Claves de archivo en el orden en que se muestran.</summary>
        public static readonly string[] ClavesOrden = { "greedy", "dc", "pd", "int1", "int2", "random", "sa", "bb" };

        public const string ClavePromedioAleatorio = "random_promedio";

        public const string MensajeSinFactibleAleatorio =
            "Sin solución factible (ninguna de las corridas aleatorias cumple E_max)";

        public static string NombreUI(string clave)
        {
            switch (clave)
            {
                case "greedy": return "Goloso";
                case "dc": return "Divide y Vencerás";
                case "pd": return "Programación Dinámica";
                case "int1": return "Integrado 1: DyV→Goloso→PD";
                case "int2": return "Integrado 2: PD reparte energía";
                case "random": return "Aleatorio";
                case ClavePromedioAleatorio: return "Aleatorio (promedio)";
                case "sa": return "Recocido Simulado";
                case "bb": return "Branch & Bound (óptimo)";
                default: return clave;
            }
        }

        /// <summary>Nombre corto para las barras del gráfico.</summary>
        public static string NombreCorto(string clave)
        {
            switch (clave)
            {
                case "greedy": return "Goloso";
                case "dc": return "DyV";
                case "pd": return "PD";
                case "int1": return "Integ. 1";
                case "int2": return "Integ. 2";
                case "random": return "Aleatorio";
                case ClavePromedioAleatorio: return "Aleat. prom.";
                case "sa": return "Recocido";
                case "bb": return "B&B";
                default: return clave;
            }
        }

        public static string NombreEtapa(Etapa e)
        {
            switch (e)
            {
                case Etapa.Corte: return "Corte";
                case Etapa.Ensamblaje: return "Ensamblaje";
                case Etapa.Empaquetado: return "Empaquetado";
                default: return e.ToString();
            }
        }

        /// <summary>
        /// Convierte el texto de "algoritmo" (del resumen o del plan) en la clave de archivo.
        /// Tolera nombres en español o inglés, con o sin tildes. Devuelve null si no lo reconoce.
        /// </summary>
        public static string ClaveDeAlgoritmo(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            string t = SinTildes(texto).ToLowerInvariant().Trim();
            string compacto = t.Replace(" ", "").Replace("_", "").Replace("-", "").Replace("(", "").Replace(")", "");
            if (t.Contains("integrado 1")) return "int1";
            if (t.Contains("integrado 2")) return "int2";
            if (compacto == "greedy" || compacto == "dc" || compacto == "pd" || compacto == "random" || compacto == "sa" || compacto == "bb")
                return compacto;

            bool aleatorio = t.Contains("aleat") || t.Contains("random") || t.Contains("azar");
            if (aleatorio)
            {
                if (t.Contains("prom") || t.Contains("media") || t.Contains("avg") || t.Contains("mean") || t.Contains("average"))
                    return ClavePromedioAleatorio;
                return "random";
            }
            if (t.Contains("golos") || t.Contains("greedy") || t.Contains("voraz")) return "greedy";
            if (t.Contains("divid") || t.Contains("divide") || compacto.Contains("dyv") || t.Contains("conquer")) return "dc";
            if (t.Contains("dinam") || t.Contains("dynamic") || compacto.StartsWith("pd") || compacto.StartsWith("dp")) return "pd";
            if (t.Contains("recoc") || t.Contains("anneal") || t.Contains("simulad") || compacto.StartsWith("sa")) return "sa";
            if (t.Contains("branch") || t.Contains("b&b") || t.Contains("ramific") || compacto.StartsWith("bb") || t.Contains("optimo")) return "bb";
            return null;
        }

        /// <summary>Quita tildes a mano (String.Normalize no es fiable en WebGL/IL2CPP).</summary>
        public static string SinTildes(string s)
        {
            const string con = "áéíóúüñÁÉÍÓÚÜÑàèìòùÀÈÌÒÙ";
            const string sin = "aeiouunAEIOUUNaeiouAEIOU";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                int i = con.IndexOf(c);
                sb.Append(i >= 0 ? sin[i] : c);
            }
            return sb.ToString();
        }

        /// <summary>Paso "bonito" (1, 2, 2.5, 5 × 10^k) para unas ~objetivo marcas entre 0 y max.</summary>
        public static double PasoMarcas(double max, int objetivo)
        {
            if (max <= 0 || objetivo <= 0) return 1;
            double bruto = max / objetivo;
            double pot = Math.Pow(10, Math.Floor(Math.Log10(bruto)));
            double f = bruto / pot;
            double paso;
            if (f <= 1) paso = 1;
            else if (f <= 2) paso = 2;
            else if (f <= 2.5) paso = 2.5;
            else if (f <= 5) paso = 5;
            else paso = 10;
            return paso * pot;
        }

        /// <summary>Lista de marcas 0, paso, 2·paso ... ≤ max.</summary>
        public static List<double> Marcas(double max, int objetivo)
        {
            var r = new List<double>();
            double paso = PasoMarcas(max, objetivo);
            for (int i = 0; i <= 1000; i++)
            {
                double v = i * paso;
                if (v > max + 1e-9) break;
                r.Add(v);
            }
            return r;
        }

        /// <summary>(costoReferencia − costoMétodo) / costoReferencia · 100. Positivo = el método es mejor.</summary>
        public static double PorcentajeMejora(double costoReferencia, double costoMetodo)
        {
            if (Math.Abs(costoReferencia) < 1e-12) return 0;
            return (costoReferencia - costoMetodo) / costoReferencia * 100.0;
        }

        /// <summary>
        /// Ids de máquina a mostrar. La CANTIDAD sale del resumen (nunca del id máximo);
        /// la base (0 o 1) se deduce de los ids que aparecen en los planes del escenario.
        /// </summary>
        public static List<int> IdsMaquinas(int cantidad, int baseIds)
        {
            var ids = new List<int>();
            for (int i = 0; i < cantidad; i++) ids.Add(baseIds + i);
            return ids;
        }

        /// <summary>0 si algún plan usa la máquina 0; 1 si todos los ids van de 1 a cantidad.</summary>
        public static int BaseIdsMaquina(IEnumerable<PlanResult> planes, int cantidad)
        {
            int min = int.MaxValue, max = int.MinValue;
            foreach (PlanResult p in planes)
            {
                if (p == null || p.asignaciones == null) continue;
                foreach (Assignment a in p.asignaciones)
                {
                    if (a.maquinaId < min) min = a.maquinaId;
                    if (a.maquinaId > max) max = a.maquinaId;
                }
            }
            if (min == int.MaxValue) return 0;
            if (min == 0) return 0;
            if (max >= cantidad) return 1;      // hay un id = cantidad: los ids empiezan en 1
            return 0;
        }

        /// <summary>Busca la fila del resumen que corresponde a una clave.</summary>
        public static FilaComparativa FilaDe(ResumenEscenario r, string clave)
        {
            if (r == null || r.filas == null) return null;
            foreach (FilaComparativa f in r.filas)
                if (ClaveDeAlgoritmo(f.algoritmo) == clave) return f;
            return null;
        }

        public static bool FilaSinSolucion(FilaComparativa f)
        {
            return f == null || f.sinSolucion;
        }

        /// <summary>Fin de la última tarea (para el eje si el makespan viniera en 0).</summary>
        public static double FinMaximo(PlanResult p)
        {
            double m = 0;
            if (p != null && p.asignaciones != null)
                foreach (Assignment a in p.asignaciones) if (a.fin > m) m = a.fin;
            return m;
        }

        public static double EscalaTiempo(PlanResult p)
        {
            double m = p != null ? Math.Max(p.makespan, FinMaximo(p)) : 0;
            return m > 0 ? m : 1;
        }

        public static string N(double v, int dec = 1)
        {
            return v.ToString("F" + dec, Ci);
        }

        public static string Ms(double ms)
        {
            if (ms < 1) return ms.ToString("F3", Ci) + " ms";
            if (ms < 1000) return ms.ToString("F1", Ci) + " ms";
            return (ms / 1000.0).ToString("F2", Ci) + " s";
        }

        public static string Signo(double v, int dec = 1)
        {
            return (v >= 0 ? "+" : "") + v.ToString("F" + dec, Ci);
        }

        /// <summary>¿El plan aleatorio tiene un mejor plan factible para comparar?</summary>
        public static bool AleatorioTieneFactible(PlanResult p)
        {
            if (p == null) return false;
            var pa = p as PlanResultAleatorio;
            if (pa != null && !pa.hayFactible && !pa.factible) return false;
            if (!p.factible) return false;
            return p.asignaciones != null && p.asignaciones.Count > 0 || p.costoTotal > 0;
        }

        /// <summary>
        /// Texto del botón "Comparar". Devuelve las líneas a mostrar.
        /// costoPromedio &lt; 0 = no hay promedio disponible.
        /// </summary>
        public static List<string> TextoComparacion(string claveMetodo, PlanResult metodo, PlanResult aleatorio, double costoPromedio, string origenPromedio)
        {
            var l = new List<string>();
            if (metodo == null) { l.Add("Primero elige un plan."); return l; }
            if (!AleatorioTieneFactible(aleatorio))
            {
                l.Add(MensajeSinFactibleAleatorio);
                if (metodo.factible) l.Add(NombreUI(claveMetodo) + ": costo " + N(metodo.costoTotal, 2) + " (sí es factible).");
                return l;
            }
            if (claveMetodo == "random")
            {
                l.Add("Este ES el mejor plan aleatorio (referencia de la comparación).");
                if (costoPromedio >= 0)
                    l.Add("Mejora vs promedio aleatorio" + origenPromedio + ": " + Signo(PorcentajeMejora(costoPromedio, metodo.costoTotal)) + " %");
                return l;
            }
            if (!metodo.factible)
            {
                l.Add(NombreUI(claveMetodo) + " no es factible (supera E_max): no se compara el costo.");
                return l;
            }
            double m = PorcentajeMejora(aleatorio.costoTotal, metodo.costoTotal);
            l.Add("Mejora vs mejor aleatorio: " + Signo(m) + " %");
            l.Add("  costo " + N(metodo.costoTotal, 2) + " vs " + N(aleatorio.costoTotal, 2) + (m >= 0 ? "  (más barato)" : "  (más caro)"));
            if (costoPromedio >= 0)
            {
                double mp = PorcentajeMejora(costoPromedio, metodo.costoTotal);
                l.Add("Mejora vs promedio aleatorio" + origenPromedio + ": " + Signo(mp) + " %");
            }
            return l;
        }
    }
}
