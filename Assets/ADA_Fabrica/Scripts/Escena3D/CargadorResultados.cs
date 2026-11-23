using System;
using Fabrica.Core;
using Fabrica.UI;

namespace Fabrica.Escena
{
    /// <summary>
    /// Persona 3: acceso a los resultados desde la escena.
    /// Integración: ya NO lee archivos (ni File.*, ni StreamingAssets, ni UnityWebRequest). Delega en
    /// RepositorioPlanes, el único lector del proyecto (Resources/Resultados + JsonUtility, válido en WebGL).
    /// Se conserva la API (CargarPlan / CargarResumen) para no romper a quien la use.
    /// </summary>
    public static class CargadorResultados
    {
        public static readonly string[] Claves = { "greedy", "dc", "pd", "random", "sa", "bb" };

        /// <summary>escenario{K}_{clave}.json, o null si no existe.</summary>
        public static PlanResult CargarPlan(int escenario, string clave)
        {
            return RepositorioPlanes.Plan(escenario, clave);
        }

        /// <summary>Versión con callback (la carga ya no es asíncrona, se llama enseguida).</summary>
        public static void CargarPlan(int escenario, string clave, Action<PlanResult> listo)
        {
            if (listo != null) listo(CargarPlan(escenario, clave));
        }

        /// <summary>resumen_comparativo.json (para el gráfico y el botón Comparar).</summary>
        public static ResumenComparativo CargarResumen()
        {
            return RepositorioPlanes.Resumen;
        }

        public static void CargarResumen(Action<ResumenComparativo> listo)
        {
            if (listo != null) listo(CargarResumen());
        }

        public static ResumenEscenario CargarResumenEscenario(int escenario)
        {
            return RepositorioPlanes.Escenario(escenario);
        }

        /// <summary>Busca el resumen del escenario de un plan suelto por su nombre ("Pequeño", "Mediano", "Grande").</summary>
        public static ResumenEscenario ResumenDePlan(PlanResult plan)
        {
            ResumenComparativo r = RepositorioPlanes.Resumen;
            if (plan == null || r == null || r.escenarios == null) return null;
            foreach (ResumenEscenario e in r.escenarios)
                if (e != null && string.Equals(e.nombre, plan.escenario, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }
    }
}
