using Fabrica.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fabrica.UI
{
    /// <summary>Paleta del panel técnico (oscuro, sobrio) y colores de etapas.</summary>
    public static class Paleta
    {
        public static readonly Color Fondo = Hex("12161CEB");
        public static readonly Color Tarjeta = Hex("1B2129");
        public static readonly Color Linea = Hex("2C3440");
        public static readonly Color FilaPar = Hex("1E252E");
        public static readonly Color FilaImpar = Hex("181E25");
        public static readonly Color Texto = Hex("E6EAF0");
        public static readonly Color TextoSuave = Hex("9AA5B4");
        public static readonly Color Acento = Hex("F2C14E");      // método actual / cursor
        public static readonly Color Neutro = Hex("5F7A96");      // barras del resto de métodos
        public static readonly Color SinSolucion = Hex("4A515C");
        public static readonly Color Bien = Hex("4CC38A");
        public static readonly Color Mal = Hex("E5606A");
        public static readonly Color Boton = Hex("2A3442");

        // Tres etapas bien distintas (azul / naranja / violeta; distinguibles también con daltonismo rojo-verde)
        public static readonly Color Corte = Hex("4C8FE8");
        public static readonly Color Ensamblaje = Hex("EE8A3C");
        public static readonly Color Empaquetado = Hex("B07FDE");

        public static Color DeEtapa(Etapa e)
        {
            switch (e)
            {
                case Etapa.Corte: return Corte;
                case Etapa.Ensamblaje: return Ensamblaje;
                default: return Empaquetado;
            }
        }

        public static Color Hex(string h)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + h, out c);
            return c;
        }

        public static string HexDe(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }
    }

    /// <summary>Atajos para crear elementos de UI (los usan los componentes y el armador del editor).</summary>
    public static class UIFabrica
    {
        public static RectTransform Nuevo(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            return rt;
        }

        /// <summary>Anclas en fracción del padre + márgenes en píxeles (izq, abajo, der, arriba).</summary>
        public static RectTransform Anclar(RectTransform rt, float xMin, float yMin, float xMax, float yMax,
                                           float mIzq = 0, float mAbajo = 0, float mDer = 0, float mArriba = 0)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(mIzq, mAbajo);
            rt.offsetMax = new Vector2(-mDer, -mArriba);
            return rt;
        }

        /// <summary>Rectángulo fijo arriba a la izquierda del padre (x, y desde la esquina superior izquierda).</summary>
        public static RectTransform ArribaIzq(RectTransform rt, float x, float y, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(ancho, alto);
            return rt;
        }

        /// <summary>Fila a lo ancho del padre, a una altura fija desde arriba.</summary>
        public static RectTransform FilaArriba(RectTransform rt, float y, float alto, float mIzq = 0, float mDer = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(mIzq, -y - alto);
            rt.offsetMax = new Vector2(-mDer, -y);
            return rt;
        }

        public static Image Imagen(string nombre, Transform padre, Color color, bool raycast = false)
        {
            RectTransform rt = Nuevo(nombre, padre);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Texto(string nombre, Transform padre, string texto, float tam,
                                            Color color, TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft,
                                            FontStyles estilo = FontStyles.Normal)
        {
            RectTransform rt = Nuevo(nombre, padre);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = tam;
            t.color = color;
            t.alignment = alineacion;
            t.fontStyle = estilo;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.richText = true;
            return t;
        }

        public static void Limpiar(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject h = t.GetChild(i).gameObject;
                h.SetActive(false);
                Object.Destroy(h);
            }
        }
    }
}
