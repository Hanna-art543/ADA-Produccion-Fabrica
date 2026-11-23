using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>
    /// Deja la vista 3D en el hueco central que no tapa la UI (panel lateral de 470 px + franja inferior de 330 px,
    /// en px de referencia 1920×1080). Ajusta el viewport (Camera.rect) de la cámara principal según la escala
    /// real del Canvas, y crea una cámara de fondo que limpia el resto de la pantalla.
    /// La cámara de la fábrica (VisualizadorFabrica) se reencuadra sola cuando cambia el viewport.
    /// </summary>
    public class AreaLibreCamara : MonoBehaviour
    {
        public Camera camara;
        public float izquierda = 470f;
        public float abajo = 346f;      // 330 del panel + 8 de margen inferior + 8 de separación
        public float derecha = 0f;
        public float arriba = 0f;
        public bool crearCamaraFondo = true;

        private Canvas canvas;
        private Camera fondo;

        private void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
        }

        private void LateUpdate()
        {
            if (camara == null) camara = Camera.main;
            if (camara == null || canvas == null || Screen.width <= 0 || Screen.height <= 0) return;
            float sf = canvas.scaleFactor;
            float x = Mathf.Clamp01(izquierda * sf / Screen.width);
            float y = Mathf.Clamp01(abajo * sf / Screen.height);
            float w = Mathf.Clamp01(1f - x - derecha * sf / Screen.width);
            float h = Mathf.Clamp01(1f - y - arriba * sf / Screen.height);
            var r = new Rect(x, y, Mathf.Max(0.05f, w), Mathf.Max(0.05f, h));
            if (camara.rect != r) camara.rect = r;

            if (crearCamaraFondo && fondo == null)
            {
                var go = new GameObject("Camara fondo (UI)", typeof(Camera));
                fondo = go.GetComponent<Camera>();
                fondo.clearFlags = CameraClearFlags.SolidColor;
                fondo.backgroundColor = camara.backgroundColor;
                fondo.cullingMask = 0;
                fondo.depth = camara.depth - 10f;
                fondo.rect = new Rect(0, 0, 1, 1);
            }
        }

        private void OnDisable()
        {
            if (camara != null) camara.rect = new Rect(0, 0, 1, 1);
        }
    }
}
