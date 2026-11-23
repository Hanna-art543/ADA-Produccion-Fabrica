using System.Collections.Generic;
using Fabrica.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabrica.UI
{
    /// <summary>
    /// Botón "Comparar": % de mejora del método actual vs Aleatorio
    ///   mejora = (costoAleatorio − costoMétodo) / costoAleatorio · 100
    /// con el mejor plan factible de *_random.json, y también contra el promedio aleatorio si el resumen lo trae.
    /// </summary>
    public class PanelComparar : MonoBehaviour
    {
        public Button boton;
        public TMP_Text resultado;

        private ControladorPlan controlador;

        private void Start()
        {
            controlador = ControladorPlan.Buscar();
            if (boton != null) boton.onClick.AddListener(Comparar);
            if (controlador != null) controlador.Suscribir(AlCargarPlan);
        }

        private void OnDestroy()
        {
            if (controlador != null) controlador.Desuscribir(AlCargarPlan);
        }

        private void AlCargarPlan(PlanResult p, ResumenEscenario r)
        {
            if (resultado != null)
                resultado.text = "<color=" + Paleta.HexDe(Paleta.TextoSuave) + ">Pulsa Comparar para ver la mejora frente al método Aleatorio.</color>";
        }

        public void Comparar()
        {
            if (controlador == null || controlador.PlanActual == null || resultado == null) return;
            int esc = controlador.EscenarioActual;
            PlanResult metodo = controlador.PlanActual;
            PlanResult aleatorio = RepositorioPlanes.Plan(esc, "random");

            // promedio aleatorio: primero la fila del resumen; si no hay, el promedio de corridas factibles del JSON
            double prom = -1;
            string origen = "";
            FilaComparativa fp = CalculosFabrica.FilaDe(controlador.ResumenActual, CalculosFabrica.ClavePromedioAleatorio);
            if (fp != null && !fp.sinSolucion && fp.costo > 0) { prom = fp.costo; origen = ""; }
            else
            {
                var pa = aleatorio as PlanResultAleatorio;
                if (pa != null && pa.corridasFactibles > 0 && pa.costoPromedioFactibles > 0)
                {
                    prom = pa.costoPromedioFactibles;
                    origen = " (prom. de corridas factibles)";
                }
            }

            List<string> l = CalculosFabrica.TextoComparacion(controlador.ClaveActual, metodo, aleatorio, prom, origen);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < l.Count; i++)
            {
                string linea = l[i];
                if (linea.StartsWith("Mejora"))
                {
                    bool positivo = linea.Contains(": +");
                    int dos = linea.LastIndexOf(':');
                    linea = linea.Substring(0, dos + 1) + " <b><size=125%><color=" +
                            Paleta.HexDe(positivo ? Paleta.Bien : Paleta.Mal) + ">" + linea.Substring(dos + 2) + "</color></size></b>";
                }
                else if (linea.StartsWith("Sin solución"))
                {
                    linea = "<color=" + Paleta.HexDe(Paleta.Acento) + ">" + linea + "</color>";
                }
                sb.Append(linea);
                if (i < l.Count - 1) sb.Append('\n');
            }
            resultado.text = sb.ToString();
        }
    }
}
