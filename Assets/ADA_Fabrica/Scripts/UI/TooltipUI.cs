using TMPro;
using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>Recuadro flotante que sigue al mouse (lo usa el Gantt). No bloquea clics.</summary>
    public class TooltipUI : MonoBehaviour
    {
        public RectTransform panel;
        public TMP_Text texto;
        public Vector2 desfase = new Vector2(16f, -16f);

        private RectTransform canvasRect;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            Ocultar();
        }

        public void Mostrar(string contenido, Vector2 posPantalla)
        {
            if (panel == null || texto == null) return;
            texto.text = contenido;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            Mover(posPantalla);
        }

        public void Mover(Vector2 posPantalla)
        {
            if (panel == null || canvasRect == null || !panel.gameObject.activeSelf) return;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, posPantalla, cam, out local)) return;

            // tamaño según el texto
            Vector2 pref = texto.GetPreferredValues(texto.text, 320f, 0f);
            Vector2 tam = new Vector2(Mathf.Min(pref.x, 320f) + 20f, pref.y + 14f);
            panel.sizeDelta = tam;

            // que no se salga del canvas: si no cabe a la derecha/abajo, se pone al otro lado
            Rect r = canvasRect.rect;
            Vector2 p = local + desfase;
            if (p.x + tam.x > r.xMax) p.x = local.x - desfase.x - tam.x;
            if (p.y - tam.y < r.yMin) p.y = local.y - desfase.y + tam.y;
            panel.pivot = new Vector2(0f, 1f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = p;
        }

        public void Ocultar()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }
    }
}
