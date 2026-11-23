using System.Collections.Generic;
using System.Text;
using Fabrica.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fabrica.UI
{
    /// <summary>
    /// Diagrama de Gantt por máquina hecho con Images de UI.
    ///  · una fila por máquina (TODAS las del resumen, aunque el algoritmo no use alguna)
    ///  · barras [inicio, fin] coloreadas por ETAPA, con el número de pieza si cabe
    ///  · eje X de 0 a makespan con marcas, y cursor que sigue a IRelojSimulacion.Tiempo
    /// Va en el objeto del área de dibujo (necesita una Image transparente para recibir clics).
    /// </summary>
    public class GanttChart : MonoBehaviour, IPointerClickHandler
    {
        [Header("Referencias (las asigna el armador del prefab)")]
        public RectTransform area;
        public RectTransform columnaFilas;
        public RectTransform ejeX;
        public TMP_Text titulo;
        public TMP_Text mensaje;
        public RectTransform cursor;
        public TMP_Text cursorTexto;
        public TooltipUI tooltip;
        [Tooltip("Opcional: componente que implementa IRelojSimulacion. Si está vacío se busca en la escena.")]
        public MonoBehaviour relojPreferido;

        [Header("Aspecto")]
        public float margenFila = 0.12f;          // fracción de la altura de la fila que queda libre arriba y abajo
        public float pxPorMarca = 90f;
        public float tamTextoBarra = 12f;

        private RectTransform capaFilas, capaRejilla, capaBarras;
        private readonly List<BarraGantt> barras = new List<BarraGantt>();
        private readonly List<TMP_Text> textosBarras = new List<TMP_Text>();
        private PlanResult plan;
        private double escala = 1;
        private IRelojSimulacion reloj;
        private float proximaBusquedaReloj;
        private bool sucio;
        private float anchoPrevio;
        private ControladorPlan controlador;

        private void OnEnable()
        {
            if (area == null) area = (RectTransform)transform;
            controlador = ControladorPlan.Buscar();
            if (controlador != null) controlador.Suscribir(Dibujar);
        }

        private void OnDisable()
        {
            if (controlador != null) controlador.Desuscribir(Dibujar);
        }

        // ------------------------------------------------------------------ dibujo

        public void Dibujar(PlanResult p, ResumenEscenario resumen)
        {
            plan = p;
            escala = CalculosFabrica.EscalaTiempo(p);
            if (tooltip != null) tooltip.Ocultar();
            Capas();
            UIFabrica.Limpiar(capaFilas);
            UIFabrica.Limpiar(capaRejilla);
            UIFabrica.Limpiar(capaBarras);
            if (columnaFilas != null) UIFabrica.Limpiar(columnaFilas);
            barras.Clear();
            textosBarras.Clear();

            int escenario = controlador != null && controlador.EscenarioActual > 0 ? controlador.EscenarioActual
                          : (resumen != null ? resumen.numero : 1);
            int n = RepositorioPlanes.CantidadMaquinas(escenario);
            int baseIds = RepositorioPlanes.BaseIdsMaquina(escenario);
            List<int> ids = CalculosFabrica.IdsMaquinas(n, baseIds);
            var filaDeId = new Dictionary<int, int>();
            for (int i = 0; i < ids.Count; i++) filaDeId[ids[i]] = i;

            var tareasPorFila = new int[Mathf.Max(1, n)];
            int fuera = 0;
            if (p.asignaciones != null)
                foreach (Assignment a in p.asignaciones)
                {
                    int f;
                    if (filaDeId.TryGetValue(a.maquinaId, out f)) tareasPorFila[f]++;
                    else fuera++;
                }
            if (fuera > 0) Debug.LogWarning("[ADA] " + fuera + " asignaciones tienen un maquinaId fuera de 0.." + n + " (se omiten).");

            // ---- filas (fondos alternados + etiqueta de cada máquina)
            for (int i = 0; i < n; i++)
            {
                float yMax = 1f - (float)i / n, yMin = 1f - (float)(i + 1) / n;
                Image fondo = UIFabrica.Imagen("Fila M" + ids[i], capaFilas, i % 2 == 0 ? Paleta.FilaPar : Paleta.FilaImpar);
                UIFabrica.Anclar(fondo.rectTransform, 0, yMin, 1, yMax);
                if (columnaFilas != null)
                {
                    string extra = tareasPorFila[i] == 0 ? "  <color=" + Paleta.HexDe(Paleta.Acento) + "><size=80%>vacía</size></color>"
                                                         : "  <color=" + Paleta.HexDe(Paleta.TextoSuave) + "><size=75%>" + tareasPorFila[i] + "</size></color>";
                    TextMeshProUGUI et = UIFabrica.Texto("M" + ids[i], columnaFilas, "M" + ids[i] + extra,
                                                         n > 6 ? 13 : 15, Paleta.Texto, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                    UIFabrica.Anclar(et.rectTransform, 0, yMin, 1, yMax, 0, 0, 8, 0);
                }
            }

            // ---- barras
            if (p.asignaciones != null)
            {
                float m = Mathf.Clamp01(margenFila);
                foreach (Assignment a in p.asignaciones)
                {
                    int f;
                    if (!filaDeId.TryGetValue(a.maquinaId, out f)) continue;
                    float x0 = (float)(a.inicio / escala), x1 = (float)(a.fin / escala);
                    float yMax = 1f - ((float)f + m) / n, yMin = 1f - ((float)(f + 1) - m) / n;
                    Color c = Paleta.DeEtapa(a.etapa);
                    Image img = UIFabrica.Imagen("P" + a.piezaId + " " + CalculosFabrica.NombreEtapa(a.etapa), capaBarras, c, true);
                    UIFabrica.Anclar(img.rectTransform, x0, yMin, Mathf.Max(x1, x0 + 0.0005f), yMax, 0.5f, 0, 0.5f, 0);
                    BarraGantt b = img.gameObject.AddComponent<BarraGantt>();
                    b.asignacion = a;
                    b.gantt = this;
                    b.imagen = img;
                    b.colorBase = c;
                    barras.Add(b);

                    TextMeshProUGUI t = UIFabrica.Texto("n", img.transform, a.piezaId.ToString(), tamTextoBarra,
                                                        new Color(0.06f, 0.07f, 0.09f), TextAlignmentOptions.Center, FontStyles.Bold);
                    t.overflowMode = TextOverflowModes.Truncate;
                    UIFabrica.Anclar(t.rectTransform, 0, 0, 1, 1);
                    textosBarras.Add(t);
                }
            }

            // ---- mensajes
            bool vacio = p.asignaciones == null || p.asignaciones.Count == 0;
            if (mensaje != null)
            {
                mensaje.gameObject.SetActive(vacio || !p.factible);
                if (vacio)
                    mensaje.text = controlador != null && controlador.ClaveActual == "random" && !p.factible
                        ? CalculosFabrica.MensajeSinFactibleAleatorio
                        : "Este plan no tiene asignaciones" + (p.factible ? "" : " (no factible)");
                else
                    mensaje.text = "Plan NO factible: supera E_max";
                mensaje.transform.SetAsLastSibling();
            }

            if (titulo != null)
            {
                string clave = controlador != null ? controlador.ClaveActual : null;
                string nombre = clave != null ? CalculosFabrica.NombreUI(clave) : p.algoritmo;
                titulo.text = "<b>Gantt</b>  ·  " + nombre + "  ·  " + RepositorioPlanes.NombreEscenario(escenario)
                              + "  ·  " + n + " máquinas  ·  makespan " + CalculosFabrica.N(p.makespan);
            }
            sucio = true;
        }

        private void Capas()
        {
            if (capaFilas != null) return;
            capaFilas = UIFabrica.Anclar(UIFabrica.Nuevo("Filas", area), 0, 0, 1, 1);
            capaRejilla = UIFabrica.Anclar(UIFabrica.Nuevo("Rejilla", area), 0, 0, 1, 1);
            capaBarras = UIFabrica.Anclar(UIFabrica.Nuevo("Barras", area), 0, 0, 1, 1);
            capaFilas.SetSiblingIndex(0);
            capaRejilla.SetSiblingIndex(1);
            capaBarras.SetSiblingIndex(2);
        }

        /// <summary>Marcas del eje y visibilidad de los números según el ancho real en píxeles.</summary>
        private void AjustarAlAncho()
        {
            float ancho = area.rect.width;
            if (ancho <= 1f) return;
            anchoPrevio = ancho;
            sucio = false;

            UIFabrica.Limpiar(capaRejilla);
            if (ejeX != null) UIFabrica.Limpiar(ejeX);
            int objetivo = Mathf.Clamp(Mathf.RoundToInt(ancho / pxPorMarca), 2, 20);
            List<double> marcas = CalculosFabrica.Marcas(escala, objetivo);
            double paso = marcas.Count > 1 ? marcas[1] : escala;
            int dec = paso >= 1 ? 0 : (paso >= 0.1 ? 1 : 2);
            foreach (double v in marcas)
            {
                float x = (float)(v / escala);
                Image l = UIFabrica.Imagen("|" + v, capaRejilla, new Color(1, 1, 1, 0.07f));
                UIFabrica.Anclar(l.rectTransform, x, 0, x, 1, -0.5f, 0, -0.5f, 0);
                if (ejeX != null)
                {
                    TextMeshProUGUI t = UIFabrica.Texto(CalculosFabrica.N(v, dec), ejeX, CalculosFabrica.N(v, dec), 12, Paleta.TextoSuave, TextAlignmentOptions.Top);
                    UIFabrica.Anclar(t.rectTransform, x, 0, x, 1, -40, 0, -40, 0);
                }
            }
            // marca final con el makespan exacto
            if (ejeX != null && plan != null)
            {
                Image l = UIFabrica.Imagen("fin", capaRejilla, new Color(1, 1, 1, 0.25f));
                UIFabrica.Anclar(l.rectTransform, 1, 0, 1, 1, -1, 0, 0, 0);
            }

            for (int i = 0; i < barras.Count; i++)
            {
                Assignment a = barras[i].asignacion;
                float px = (float)((a.fin - a.inicio) / escala) * ancho;
                int digitos = a.piezaId.ToString().Length;
                textosBarras[i].gameObject.SetActive(px >= 6f + 7.5f * digitos);
            }
        }

        // ------------------------------------------------------------------ cursor del reloj

        private void LateUpdate()
        {
            if (area == null) return;
            if (sucio || Mathf.Abs(area.rect.width - anchoPrevio) > 0.5f) AjustarAlAncho();

            if (reloj == null && Time.unscaledTime >= proximaBusquedaReloj)
            {
                proximaBusquedaReloj = Time.unscaledTime + 1f;
                reloj = BuscadorReloj.Buscar<IRelojSimulacion>(relojPreferido);
            }
            if (cursor == null) return;
            bool hay = reloj != null && plan != null && (reloj as MonoBehaviour) != null;
            if (cursor.gameObject.activeSelf != hay) cursor.gameObject.SetActive(hay);
            if (!hay) { reloj = null; return; }

            double t = reloj.Tiempo;
            float x = Mathf.Clamp01((float)(t / escala));
            cursor.anchorMin = new Vector2(x, cursor.anchorMin.y);
            cursor.anchorMax = new Vector2(x, cursor.anchorMax.y);
            cursor.anchoredPosition = new Vector2(0f, cursor.anchoredPosition.y);
            cursor.SetAsLastSibling();
            if (cursorTexto != null) cursorTexto.text = "t = " + CalculosFabrica.N(t);
        }

        // ------------------------------------------------------------------ interacción

        public void AlEntrarBarra(BarraGantt b, Vector2 pos)
        {
            foreach (BarraGantt o in barras)
            {
                Color c = o.colorBase;
                if (o.asignacion.piezaId != b.asignacion.piezaId) c.a = 0.28f;
                o.imagen.color = c;
            }
            b.imagen.color = Color.Lerp(b.colorBase, Color.white, 0.35f);
            if (tooltip != null) tooltip.Mostrar(TextoTooltip(b.asignacion), pos);
        }

        public void AlMoverBarra(Vector2 pos)
        {
            if (tooltip != null) tooltip.Mover(pos);
        }

        public void AlSalirBarra(BarraGantt b)
        {
            foreach (BarraGantt o in barras) o.imagen.color = o.colorBase;
            if (tooltip != null) tooltip.Ocultar();
        }

        private string TextoTooltip(Assignment a)
        {
            var sb = new StringBuilder();
            sb.Append("<b>Pieza ").Append(a.piezaId).Append("</b>   <color=").Append(Paleta.HexDe(Paleta.TextoSuave))
              .Append(">tarea ").Append(a.jobId).Append("</color>\n");
            sb.Append("<color=").Append(Paleta.HexDe(Paleta.DeEtapa(a.etapa))).Append("><b>")
              .Append(CalculosFabrica.NombreEtapa(a.etapa)).Append("</b></color>  ·  Máquina M").Append(a.maquinaId).Append('\n');
            sb.Append("Inicio ").Append(CalculosFabrica.N(a.inicio, 2)).Append("   Fin ").Append(CalculosFabrica.N(a.fin, 2))
              .Append("   (dura ").Append(CalculosFabrica.N(a.fin - a.inicio, 2)).Append(")\n");
            sb.Append("Energía ").Append(CalculosFabrica.N(a.energia, 2));
            return sb.ToString();
        }

        /// <summary>Clic en el Gantt: mueve el reloj a ese tiempo si el reloj lo permite.</summary>
        public void OnPointerClick(PointerEventData e)
        {
            AlClicEnTiempo(e);
        }

        public void AlClicEnTiempo(PointerEventData e)
        {
            var buscable = reloj as IRelojBuscable;
            if (buscable == null || area == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out local)) return;
            float f = Mathf.InverseLerp(area.rect.xMin, area.rect.xMax, local.x);
            buscable.IrA(f * escala);
        }
    }
}
