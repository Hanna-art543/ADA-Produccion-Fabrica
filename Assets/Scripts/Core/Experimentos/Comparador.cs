using System.Globalization;
using System.Text;

namespace Fabrica.Core
{
    // Formatea la tabla comparativa en texto plano (para consola o Debug.Log).
    public static class Comparador
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Tabla(ResumenComparativo resumen)
        {
            var sb = new StringBuilder();
            foreach (var r in resumen.escenarios) sb.Append(Tabla(r)).AppendLine();
            sb.AppendLine("Nota: " + resumen.nota);
            return sb.ToString();
        }

        public static string Tabla(ResumenEscenario r)
        {
            var sb = new StringBuilder();
            bool optimo = r.tipoReferencia == "optimo B&B";
            sb.AppendLine(string.Format(Inv,
                "=== Escenario {0}: {1} ({2} piezas, {3} tareas, {4} máquinas) | E_max = {5:F2} | α = {6} β = {7} ===",
                r.numero, r.nombre, r.piezas, r.tareas, r.maquinas, r.energiaMax, r.alfa, r.beta));
            sb.AppendLine(optimo
                ? string.Format(Inv, "Referencia: óptimo de Branch & Bound, costo = {0:F2}", r.costoReferencia)
                : string.Format(Inv, "Referencia: COTA INFERIOR (no es el óptimo), costo ≥ {0:F2}", r.costoReferencia));

            string gapTit = optimo ? "Gap vs ópt (%)" : "Gap vs cota (%)";
            string enc = string.Format("{0,-27} | {1,9} | {2,9} | {3,9} | {4,15} | {5,11} | {6}",
                                       "Algoritmo", "Makespan", "Energía", "Costo", gapTit, "Tiempo (ms)", "Factible");
            sb.AppendLine(enc);
            sb.AppendLine(new string('-', enc.Length));
            foreach (var f in r.filas)
            {
                if (f.sinSolucion)
                {
                    sb.AppendLine(string.Format(Inv, "{0,-27} | {1,-51} | {2,11:F3} | -", f.algoritmo, "sin solución factible", f.tiempoMs));
                    continue;
                }
                sb.AppendLine(string.Format(Inv, "{0,-27} | {1,9:F2} | {2,9:F2} | {3,9:F2} | {4,15:F2} | {5,11:F3} | {6}",
                    f.algoritmo, f.makespan, f.energia, f.costo, f.gapPorcentaje, f.tiempoMs, f.factible ? "sí" : "NO"));
            }
            foreach (var n in r.notas) sb.AppendLine(n);
            return sb.ToString();
        }
    }
}
