using System.Collections.Generic;
using Fabrica.Core;
using TMPro;
using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>
    /// Dropdowns de escenario y método (solo las claves que existen para ese escenario) y ficha del plan:
    /// algoritmo, makespan, energía / E_max, costo, factible y tiempo de ejecución.
    /// </summary>
    public class PanelSeleccion : MonoBehaviour
    {
        public TMP_Dropdown escenario;
        public TMP_Dropdown metodo;
        public TMP_Text algoritmo;
        public TMP_Text detalleEscenario;
        public TMP_Text metricas;
        public RectTransform barraEnergia;     // relleno: su ancla max x = energía / E_max
        public UnityEngine.UI.Image rellenoEnergia;
        public TMP_Text notas;

        private ControladorPlan controlador;
        private readonly List<string> clavesMetodo = new List<string>();
        private bool listo;
        private int escenarioMetodos = -1;

        private void Start()
        {
            controlador = ControladorPlan.Buscar();
            if (controlador == null) { Debug.LogError("[ADA] Falta ControladorPlan en la escena."); return; }

            escenario.ClearOptions();
            var ops = new List<string>();
            int n = RepositorioPlanes.CantidadEscenarios;
            for (int i = 1; i <= n; i++)
            {
                ResumenEscenario e = RepositorioPlanes.Escenario(i);
                string txt = i + " · " + RepositorioPlanes.NombreEscenario(i);
                if (e != null) txt += "  (" + e.piezas + " piezas, " + e.maquinas + " máq.)";
                ops.Add(txt);
            }
            escenario.AddOptions(ops);
            escenario.onValueChanged.AddListener(AlCambiarEscenario);
            metodo.onValueChanged.AddListener(AlCambiarMetodo);

            listo = true;
            controlador.Suscribir(AlCargarPlan);
            if (controlador.PlanActual == null)
            {
                int e0 = Mathf.Clamp(controlador.escenarioInicial, 1, n);
                LlenarMetodos(e0);
                escenario.SetValueWithoutNotify(e0 - 1);
                string c0 = clavesMetodo.Contains(controlador.claveInicial) ? controlador.claveInicial : (clavesMetodo.Count > 0 ? clavesMetodo[0] : null);
                if (c0 != null) controlador.CargarPlan(e0, c0);
            }
        }

        private void OnDestroy()
        {
            if (controlador != null) controlador.Desuscribir(AlCargarPlan);
        }

        private void LlenarMetodos(int esc)
        {
            escenarioMetodos = esc;
            clavesMetodo.Clear();
            clavesMetodo.AddRange(RepositorioPlanes.ClavesDisponibles(esc));
            metodo.ClearOptions();
            var ops = new List<string>();
            foreach (string c in clavesMetodo) ops.Add(CalculosFabrica.NombreUI(c));
            if (ops.Count == 0) ops.Add("(sin planes)");
            metodo.AddOptions(ops);
        }

        private void AlCambiarEscenario(int indice)
        {
            int esc = indice + 1;
            string anterior = controlador.ClaveActual;
            LlenarMetodos(esc);
            if (clavesMetodo.Count == 0) return;
            string clave = clavesMetodo.Contains(anterior) ? anterior : clavesMetodo[0];
            metodo.SetValueWithoutNotify(clavesMetodo.IndexOf(clave));
            controlador.CargarPlan(esc, clave);
        }

        private void AlCambiarMetodo(int indice)
        {
            if (indice < 0 || indice >= clavesMetodo.Count) return;
            controlador.CargarPlan(escenario.value + 1, clavesMetodo[indice]);
        }

        /// <summary>También se actualiza si otro script (la escena 3D) llama a CargarPlan.</summary>
        private void AlCargarPlan(PlanResult p, ResumenEscenario r)
        {
            if (!listo) return;
            int esc = controlador.EscenarioActual;
            if (escenario.value != esc - 1) escenario.SetValueWithoutNotify(esc - 1);
            if (escenarioMetodos != esc) LlenarMetodos(esc);
            int im = clavesMetodo.IndexOf(controlador.ClaveActual);
            if (im >= 0 && metodo.value != im) metodo.SetValueWithoutNotify(im);
            escenario.RefreshShownValue();
            metodo.RefreshShownValue();

            string gris = Paleta.HexDe(Paleta.TextoSuave);
            algoritmo.text = CalculosFabrica.NombreUI(controlador.ClaveActual)
                             + (string.IsNullOrEmpty(p.algoritmo) ? "" : "   <size=70%><color=" + gris + ">" + p.algoritmo + "</color></size>");

            if (detalleEscenario != null)
                detalleEscenario.text = r == null ? "" :
                    r.nombre + ": " + r.piezas + " piezas · " + r.tareas + " tareas · " + r.maquinas + " máquinas";

            double emax = p.energiaMax > 0 ? p.energiaMax : (r != null ? r.energiaMax : 0);
            bool vacio = p.asignaciones == null || p.asignaciones.Count == 0;
            var s = new System.Text.StringBuilder();
            if (vacio && !p.factible && controlador.ClaveActual == "random")
            {
                s.Append("<color=").Append(Paleta.HexDe(Paleta.Mal)).Append(">").Append(CalculosFabrica.MensajeSinFactibleAleatorio).Append("</color>\n");
            }
            else
            {
                s.Append(Linea("Makespan", CalculosFabrica.N(p.makespan, 2)));
                s.Append(Linea("Energía", CalculosFabrica.N(p.energiaTotal, 2) + "  <color=" + gris + ">/ E_max " + CalculosFabrica.N(emax, 2) + "</color>"));
                s.Append(Linea("Costo", "<b>" + CalculosFabrica.N(p.costoTotal, 2) + "</b>"));
            }
            s.Append(Linea("Factible", p.factible
                ? "<color=" + Paleta.HexDe(Paleta.Bien) + ">Sí</color>"
                : "<color=" + Paleta.HexDe(Paleta.Mal) + ">No</color>"));
            s.Append(Linea("Tiempo de ejecución", CalculosFabrica.Ms(p.tiempoEjecucionMs)));
            var pa = p as PlanResultAleatorio;
            if (pa != null && pa.corridas > 0)
                s.Append(Linea("Corridas factibles", pa.corridasFactibles + " / " + pa.corridas + "  (" + CalculosFabrica.N(pa.porcentajeFactibles, 1) + " %)"));
            metricas.text = s.ToString();

            // barra energía / E_max
            float frac = emax > 0 ? Mathf.Clamp01((float)(p.energiaTotal / emax)) : 0f;
            if (barraEnergia != null)
            {
                barraEnergia.anchorMax = new Vector2(vacio ? 0f : frac, barraEnergia.anchorMax.y);
                barraEnergia.offsetMax = new Vector2(0, barraEnergia.offsetMax.y);
            }
            if (rellenoEnergia != null)
                rellenoEnergia.color = p.energiaTotal > emax && emax > 0 ? Paleta.Mal : (frac > 0.9f ? Paleta.Ensamblaje : Paleta.Bien);

            if (notas != null) notas.text = string.IsNullOrEmpty(p.notas) ? "" : p.notas;
        }

        private static string Linea(string nombre, string valor)
        {
            return "<color=" + Paleta.HexDe(Paleta.TextoSuave) + ">" + nombre + "</color><pos=48%>" + valor + "\n";
        }
    }
}
