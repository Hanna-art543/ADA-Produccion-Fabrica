using Fabrica.Core;
using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>
    /// Reloj PROVISIONAL solo para la escena de prueba (ADA_UI_Test). Cuando llegue la escena de la
    /// Persona 3, su reloj implementa IRelojSimulacion (o IRelojControlable) y este se quita.
    /// Tiempo simulado = segundos reales × unidadesPorSegundo × velocidad.
    /// </summary>
    public class RelojSimple : MonoBehaviour, IRelojControlable, IRelojBuscable
    {
        [Tooltip("Unidades de tiempo del plan por segundo real a velocidad x1")]
        public float unidadesPorSegundo = 10f;
        [Tooltip("Si es > 0, ajusta unidadesPorSegundo para que todo el plan dure estos segundos a x1")]
        public float segundosPorPlan = 20f;
        public bool reproducirAlCargar = true;

        private double tiempo;
        private double makespan;
        private bool reproduciendo;
        private float velocidad = 1f;
        private ControladorPlan controlador;

        public double Tiempo { get { return tiempo; } }
        public double Makespan { get { return makespan; } }
        public bool Reproduciendo { get { return reproduciendo; } }

        public float Velocidad
        {
            get { return velocidad; }
            set { velocidad = Mathf.Clamp(value, 0.5f, 8f); }
        }

        private void OnEnable()
        {
            controlador = ControladorPlan.Buscar();
            if (controlador != null) controlador.Suscribir(AlCargarPlan);
        }

        private void OnDisable()
        {
            if (controlador != null) controlador.Desuscribir(AlCargarPlan);
        }

        private void AlCargarPlan(PlanResult plan, ResumenEscenario resumen)
        {
            makespan = CalculosFabrica.EscalaTiempo(plan);
            if (segundosPorPlan > 0f) unidadesPorSegundo = (float)(makespan / segundosPorPlan);
            tiempo = 0;
            reproduciendo = reproducirAlCargar && plan.asignaciones != null && plan.asignaciones.Count > 0;
        }

        private void Update()
        {
            if (!reproduciendo) return;
            tiempo += Time.unscaledDeltaTime * unidadesPorSegundo * velocidad;
            if (tiempo >= makespan)
            {
                tiempo = makespan;
                reproduciendo = false;
            }
        }

        public void Reproducir()
        {
            if (tiempo >= makespan) tiempo = 0;
            reproduciendo = makespan > 0;
        }

        public void Pausar() { reproduciendo = false; }

        public void Reiniciar()
        {
            tiempo = 0;
            reproduciendo = false;
        }

        /// <summary>Para arrastrar el cursor del Gantt (opcional).</summary>
        public void IrA(double t)
        {
            tiempo = t < 0 ? 0 : (t > makespan ? makespan : t);
        }
    }
}
