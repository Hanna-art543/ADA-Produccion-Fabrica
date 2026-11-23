using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fabrica.UI
{
    /// <summary>
    /// Barra provisional de reloj (play / pausa / reiniciar / velocidad x0.5–x8).
    /// Solo se muestra si en la escena hay un reloj que implemente IRelojControlable (RelojSimple en la prueba).
    /// Si la escena 3D trae sus propios controles, basta con desactivar este objeto.
    /// </summary>
    public class ControlesReloj : MonoBehaviour
    {
        public Button reproducir;
        public TMP_Text textoReproducir;
        public Button reiniciar;
        public Button masLento;
        public Button masRapido;
        public TMP_Text textoVelocidad;
        public TMP_Text textoTiempo;
        public GameObject grupoBotones;
        [Tooltip("Opcional: el reloj a controlar. Si está vacío se busca en la escena.")]
        public MonoBehaviour relojPreferido;

        private static readonly float[] Velocidades = { 0.5f, 1f, 2f, 4f, 8f };
        private IRelojSimulacion reloj;
        private IRelojControlable control;
        private float proximaBusqueda;

        private void Start()
        {
            if (reproducir != null) reproducir.onClick.AddListener(() => { if (control == null) return; if (control.Reproduciendo) control.Pausar(); else control.Reproducir(); });
            if (reiniciar != null) reiniciar.onClick.AddListener(() => { if (control != null) control.Reiniciar(); });
            if (masLento != null) masLento.onClick.AddListener(() => CambiarVelocidad(-1));
            if (masRapido != null) masRapido.onClick.AddListener(() => CambiarVelocidad(+1));
        }

        private void CambiarVelocidad(int d)
        {
            if (control == null) return;
            int i = 0;
            for (int k = 0; k < Velocidades.Length; k++) if (Mathf.Approximately(Velocidades[k], control.Velocidad)) i = k;
            i = Mathf.Clamp(i + d, 0, Velocidades.Length - 1);
            control.Velocidad = Velocidades[i];
        }

        private void Update()
        {
            if ((reloj == null || (reloj as MonoBehaviour) == null) && Time.unscaledTime >= proximaBusqueda)
            {
                proximaBusqueda = Time.unscaledTime + 1f;
                reloj = BuscadorReloj.Buscar<IRelojSimulacion>(relojPreferido);
                control = reloj as IRelojControlable;
            }
            bool hayControl = control != null && (control as MonoBehaviour) != null;
            if (grupoBotones != null && grupoBotones.activeSelf != hayControl) grupoBotones.SetActive(hayControl);

            if (textoTiempo != null)
                textoTiempo.text = reloj != null && (reloj as MonoBehaviour) != null
                    ? "t = " + CalculosFabrica.N(reloj.Tiempo) + " / " + CalculosFabrica.N(reloj.Makespan)
                    : "sin reloj de simulación";
            if (hayControl)
            {
                if (textoReproducir != null) textoReproducir.text = control.Reproduciendo ? "Pausa" : "Play";
                if (textoVelocidad != null) textoVelocidad.text = "x" + (control.Velocidad < 1f ? "0.5" : control.Velocidad.ToString("0"));
            }
        }
    }
}
