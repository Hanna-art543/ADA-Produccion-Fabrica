using System.Collections.Generic;
using Fabrica.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabrica.UI
{
    /// <summary>
    /// Gráfico de barras del escenario actual (resumen.filas): tres grupos (costo, makespan, energía),
    /// una barra por algoritmo, el método actual resaltado y "sin solución" marcado aparte.
    /// En costo se muestra el gap % contra la referencia (óptimo B&B o cota inferior).
    /// </summary>
    public class GraficoComparativo : MonoBehaviour
    {
        public RectTransform contenido;
        public TMP_Text referencia;

        [Header("Columnas (px)")]
        public float anchoEtiqueta = 92f;
        public float anchoValor = 112f;
        public float altoTitulo = 20f;
        public float altoFilaMax = 18f;

        private ControladorPlan controlador;

        private struct Metrica
        {
            public string titulo;
            public System.Func<FilaComparativa, double> valor;
            public double lineaRef;
            public string nombreRef;
        }

        private void OnEnable()
        {
            if (contenido == null) contenido = (RectTransform)transform;
            controlador = ControladorPlan.Buscar();
            if (controlador != null) controlador.Suscribir(Dibujar);
        }

        private void OnDisable()
        {
            if (controlador != null) controlador.Desuscribir(Dibujar);
        }

        public void Dibujar(PlanResult plan, ResumenEscenario r)
        {
            UIFabrica.Limpiar(contenido);
            if (r == null || r.filas == null || r.filas.Count == 0)
            {
                if (referencia != null) referencia.text = "Sin datos de resumen para este escenario.";
                return;
            }
            string claveActual = controlador != null ? controlador.ClaveActual : CalculosFabrica.ClaveDeAlgoritmo(plan.algoritmo);

            if (referencia != null)
            {
                string tipo = string.IsNullOrEmpty(r.tipoReferencia) ? "referencia" : r.tipoReferencia;
                string txt = "Gap vs <b>" + tipo + "</b> = " + CalculosFabrica.N(r.costoReferencia, 2);
                if (r.cotaInferior > 0 && !tipo.ToLowerInvariant().Contains("cota"))
                    txt += "   <color=" + Paleta.HexDe(Paleta.TextoSuave) + ">(cota inf. " + CalculosFabrica.N(r.cotaInferior, 2) + ")</color>";
                txt += "\n<size=85%><color=" + Paleta.HexDe(Paleta.TextoSuave) + ">costo = α·makespan + β·energía   (α=" +
                       CalculosFabrica.N(r.alfa, 2) + ", β=" + CalculosFabrica.N(r.beta, 2) + ")</color></size>";
                referencia.text = txt;
            }

            var metricas = new List<Metrica>
            {
                new Metrica { titulo = "Costo", valor = f => f.costo, lineaRef = r.costoReferencia, nombreRef = "ref" },
                new Metrica { titulo = "Makespan", valor = f => f.makespan, lineaRef = -1, nombreRef = null },
                new Metrica { titulo = "Energía", valor = f => f.energia, lineaRef = r.energiaMax, nombreRef = "E_max" },
            };

            int g = metricas.Count;
            for (int i = 0; i < g; i++)
            {
                RectTransform grupo = UIFabrica.Anclar(UIFabrica.Nuevo("Grupo " + metricas[i].titulo, contenido),
                                                       0, 1f - (float)(i + 1) / g, 1, 1f - (float)i / g, 0, 4, 0, 0);
                DibujarGrupo(grupo, metricas[i], r, claveActual, i == 0);
            }
        }

        private void DibujarGrupo(RectTransform grupo, Metrica m, ResumenEscenario r, string claveActual, bool conGap)
        {
            TextMeshProUGUI t = UIFabrica.Texto("Titulo", grupo, m.titulo, 14, Paleta.Texto, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UIFabrica.FilaArriba(t.rectTransform, 0, altoTitulo);

            double max = 0;
            foreach (FilaComparativa f in r.filas) if (!f.sinSolucion && m.valor(f) > max) max = m.valor(f);
            if (m.lineaRef > max) max = m.lineaRef;
            max = max > 0 ? max * 1.04 : 1;

            float alto = grupo.rect.height > 0 ? grupo.rect.height : 120f;
            int n = r.filas.Count;
            float altoFila = Mathf.Min(altoFilaMax, (alto - altoTitulo - 4f) / Mathf.Max(1, n));

            // zona de barras (para la línea de referencia)
            RectTransform zona = UIFabrica.Nuevo("Zona", grupo);
            zona.anchorMin = new Vector2(0, 1);
            zona.anchorMax = new Vector2(1, 1);
            zona.pivot = new Vector2(0.5f, 1);
            zona.offsetMin = new Vector2(anchoEtiqueta + 6f, -altoTitulo - 2f - altoFila * n);
            zona.offsetMax = new Vector2(-anchoValor, -altoTitulo - 2f);

            for (int k = 0; k < n; k++)
            {
                FilaComparativa f = r.filas[k];
                string clave = CalculosFabrica.ClaveDeAlgoritmo(f.algoritmo);
                bool actual = clave != null && clave == claveActual;
                float y = altoTitulo + 2f + k * altoFila;

                // etiqueta
                string nombre = clave != null ? CalculosFabrica.NombreCorto(clave) : f.algoritmo;
                TextMeshProUGUI et = UIFabrica.Texto("Etiqueta", grupo, nombre, 12.5f,
                                                     actual ? Paleta.Acento : Paleta.TextoSuave, TextAlignmentOptions.MidlineRight,
                                                     actual ? FontStyles.Bold : FontStyles.Normal);
                RectTransform ert = et.rectTransform;
                ert.anchorMin = ert.anchorMax = new Vector2(0, 1);
                ert.pivot = new Vector2(0, 1);
                ert.anchoredPosition = new Vector2(0, -y);
                ert.sizeDelta = new Vector2(anchoEtiqueta, altoFila);

                // valor
                string valor;
                if (f.sinSolucion) valor = "<i>sin solución</i>";
                else
                {
                    valor = CalculosFabrica.N(m.valor(f), 1);
                    if (conGap) valor += "  <size=80%><color=" + Paleta.HexDe(Paleta.TextoSuave) + ">" +
                                         CalculosFabrica.Signo(f.gapPorcentaje) + "%</color></size>";
                    if (!f.factible) valor += " <color=" + Paleta.HexDe(Paleta.Mal) + "><size=80%>no fact.</size></color>";
                }
                TextMeshProUGUI vt = UIFabrica.Texto("Valor", grupo, valor, 12,
                                                     f.sinSolucion ? Paleta.TextoSuave : (actual ? Paleta.Acento : Paleta.Texto),
                                                     TextAlignmentOptions.MidlineLeft);
                RectTransform vrt = vt.rectTransform;
                vrt.anchorMin = vrt.anchorMax = new Vector2(1, 1);
                vrt.pivot = new Vector2(1, 1);
                vrt.anchoredPosition = new Vector2(0, -y);
                vrt.sizeDelta = new Vector2(anchoValor - 6f, altoFila);

                // barra
                float y0 = 1f - (float)(k + 1) / n, y1 = 1f - (float)k / n;
                if (f.sinSolucion)
                {
                    Image fondo = UIFabrica.Imagen("SinSolucion", zona, Paleta.SinSolucion);
                    UIFabrica.Anclar(fondo.rectTransform, 0, y0, 1, y1, 0, 2, 0, 2);
                    Color c = fondo.color; c.a = 0.35f; fondo.color = c;
                    TextMeshProUGUI st = UIFabrica.Texto("txt", fondo.transform, "sin solución factible", 11, Paleta.TextoSuave,
                                                         TextAlignmentOptions.Center, FontStyles.Italic);
                    UIFabrica.Anclar(st.rectTransform, 0, 0, 1, 1);
                    continue;
                }
                float frac = Mathf.Clamp01((float)(m.valor(f) / max));
                Color col = actual ? Paleta.Acento : (clave == CalculosFabrica.ClavePromedioAleatorio ? Color.Lerp(Paleta.Neutro, Paleta.Fondo, 0.35f) : Paleta.Neutro);
                if (!f.factible && !actual) col = Color.Lerp(Paleta.Mal, Paleta.Fondo, 0.3f);
                Image barra = UIFabrica.Imagen("Barra", zona, col);
                UIFabrica.Anclar(barra.rectTransform, 0, y0, Mathf.Max(frac, 0.003f), y1, 0, 2, 0, 2);
            }

            // línea de referencia (costo de referencia / E_max)
            if (m.lineaRef > 0)
            {
                float x = Mathf.Clamp01((float)(m.lineaRef / max));
                Image l = UIFabrica.Imagen("Linea " + m.nombreRef, zona, m.nombreRef == "E_max" ? Paleta.Mal : Paleta.Texto);
                UIFabrica.Anclar(l.rectTransform, x, 0, x, 1, -1, -2, -1, -2);
                TextMeshProUGUI lt = UIFabrica.Texto("t", grupo, m.nombreRef, 10.5f, l.color, TextAlignmentOptions.BottomRight);
                RectTransform lrt = lt.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1);
                lrt.pivot = new Vector2(1, 1);
                float anchoZona = grupo.rect.width - anchoEtiqueta - 6f - anchoValor;
                lrt.anchoredPosition = new Vector2(anchoEtiqueta + 6f + x * Mathf.Max(0f, anchoZona) + 30f, 0);
                lrt.sizeDelta = new Vector2(60f, altoTitulo);
            }
        }
    }
}
