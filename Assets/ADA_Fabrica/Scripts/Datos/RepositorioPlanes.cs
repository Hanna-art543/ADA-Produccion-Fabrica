using System.Collections.Generic;
using Fabrica.Core;
using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>
    /// Carga y cachea los planes y el resumen desde Resources/Resultados (JsonUtility; sin System.IO,
    /// así funciona en WebGL). Archivos: escenario{N}_{clave}.json y resumen_comparativo.json.
    /// </summary>
    public static class RepositorioPlanes
    {
        public const string Carpeta = "Resultados/";
        public static readonly string[] NombresEscenario = { "Pequeño", "Mediano", "Grande" };

        private static ResumenComparativo resumen;
        private static bool resumenIntentado;
        private static readonly Dictionary<string, PlanResult> cache = new Dictionary<string, PlanResult>();
        private static readonly Dictionary<int, List<string>> clavesPorEscenario = new Dictionary<int, List<string>>();
        private static readonly Dictionary<int, int> baseIds = new Dictionary<int, int>();

        public static int CantidadEscenarios
        {
            get
            {
                ResumenComparativo r = Resumen;
                return r != null && r.escenarios != null && r.escenarios.Count > 0 ? r.escenarios.Count : 3;
            }
        }

        public static ResumenComparativo Resumen
        {
            get
            {
                if (!resumenIntentado)
                {
                    resumenIntentado = true;
                    TextAsset ta = Resources.Load<TextAsset>(Carpeta + "resumen_comparativo");
                    if (ta == null) Debug.LogError("[ADA] Falta Resources/Resultados/resumen_comparativo.json (menú ADA Fabrica → Importar Resultados).");
                    else resumen = JsonUtility.FromJson<ResumenComparativo>(Texto(ta));
                }
                return resumen;
            }
        }

        /// <summary>Resumen del escenario N (1..3). Busca por "numero" y, si no, por posición.</summary>
        public static ResumenEscenario Escenario(int n)
        {
            ResumenComparativo r = Resumen;
            if (r == null || r.escenarios == null) return null;
            foreach (ResumenEscenario e in r.escenarios) if (e != null && e.numero == n) return e;
            return n - 1 >= 0 && n - 1 < r.escenarios.Count ? r.escenarios[n - 1] : null;
        }

        public static string NombreEscenario(int n)
        {
            ResumenEscenario e = Escenario(n);
            if (e != null && !string.IsNullOrEmpty(e.nombre)) return e.nombre;
            return n >= 1 && n <= NombresEscenario.Length ? NombresEscenario[n - 1] : "Escenario " + n;
        }

        public static string Ruta(int escenario, string clave)
        {
            return Carpeta + "escenario" + escenario + "_" + clave;
        }

        public static bool Existe(int escenario, string clave)
        {
            return ClavesDisponibles(escenario).Contains(clave);
        }

        /// <summary>Claves con archivo para ese escenario, en el orden de la UI.</summary>
        public static List<string> ClavesDisponibles(int escenario)
        {
            List<string> l;
            if (clavesPorEscenario.TryGetValue(escenario, out l)) return l;
            l = new List<string>();
            foreach (string c in CalculosFabrica.ClavesOrden)
            {
                if (cache.ContainsKey(Ruta(escenario, c)) || Resources.Load<TextAsset>(Ruta(escenario, c)) != null) l.Add(c);
            }
            clavesPorEscenario[escenario] = l;
            return l;
        }

        /// <summary>Plan del escenario y clave (los *_random se leen como PlanResultAleatorio). null si no existe.</summary>
        public static PlanResult Plan(int escenario, string clave)
        {
            string ruta = Ruta(escenario, clave);
            PlanResult p;
            if (cache.TryGetValue(ruta, out p)) return p;
            TextAsset ta = Resources.Load<TextAsset>(ruta);
            if (ta == null) return null;
            if (clave == "random") p = JsonUtility.FromJson<PlanResultAleatorio>(Texto(ta));
            else p = JsonUtility.FromJson<PlanResult>(Texto(ta));
            if (p != null && p.asignaciones == null) p.asignaciones = new List<Assignment>();
            cache[ruta] = p;
            return p;
        }

        public static PlanResultAleatorio PlanAleatorio(int escenario)
        {
            return Plan(escenario, "random") as PlanResultAleatorio;
        }

        /// <summary>Base de los ids de máquina (0 o 1) del escenario, mirando todos sus planes.</summary>
        public static int BaseIdsMaquina(int escenario)
        {
            int b;
            if (baseIds.TryGetValue(escenario, out b)) return b;
            var planes = new List<PlanResult>();
            foreach (string c in ClavesDisponibles(escenario)) planes.Add(Plan(escenario, c));
            ResumenEscenario e = Escenario(escenario);
            b = CalculosFabrica.BaseIdsMaquina(planes, e != null ? e.maquinas : 0);
            baseIds[escenario] = b;
            return b;
        }

        /// <summary>Número de máquinas: SIEMPRE del resumen (hay algoritmos que dejan máquinas sin usar).</summary>
        public static int CantidadMaquinas(int escenario)
        {
            ResumenEscenario e = Escenario(escenario);
            if (e != null && e.maquinas > 0) return e.maquinas;
            Debug.LogWarning("[ADA] El resumen no trae 'maquinas' para el escenario " + escenario + ".");
            return 0;
        }

        /// <summary>Texto del JSON sin BOM (JsonUtility falla con él).</summary>
        private static string Texto(TextAsset ta)
        {
            string s = ta.text;
            return s.Length > 0 && s[0] == '\uFEFF' ? s.Substring(1) : s;
        }

        public static void LimpiarCache()
        {
            cache.Clear();
            clavesPorEscenario.Clear();
            baseIds.Clear();
            resumen = null;
            resumenIntentado = false;
        }
    }
}
