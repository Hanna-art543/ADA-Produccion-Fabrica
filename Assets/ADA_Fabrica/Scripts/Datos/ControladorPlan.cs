using System;
using Fabrica.Core;
using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>
    /// PUNTO DE ENTRADA ÚNICO. La UI llama a CargarPlan(escenario, clave); todo lo demás
    /// (Gantt, gráfico, y después la escena 3D de la Persona 3) escucha OnPlanCargado.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ControladorPlan : MonoBehaviour
    {
        public static ControladorPlan Instancia { get; private set; }

        /// <summary>Se dispara cada vez que se carga un plan: (plan, resumen de su escenario).</summary>
        public event Action<PlanResult, ResumenEscenario> OnPlanCargado;

        [Tooltip("Plan que se carga al iniciar")]
        public int escenarioInicial = 1;
        public string claveInicial = "greedy";
        public bool cargarAlIniciar = true;

        public PlanResult PlanActual { get; private set; }
        public ResumenEscenario ResumenActual { get; private set; }
        public int EscenarioActual { get; private set; }
        public string ClaveActual { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReiniciarEstatico()
        {
            Instancia = null;
            RepositorioPlanes.LimpiarCache();     // por si "Enter Play Mode Options" no recarga el dominio
        }

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Debug.LogWarning("[ADA] Hay más de un ControladorPlan en la escena; se usa el primero.");
                return;
            }
            Instancia = this;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        private void Start()
        {
            if (cargarAlIniciar && PlanActual == null) CargarPlan(escenarioInicial, claveInicial);
        }

        /// <summary>Carga el plan y avisa a todos. Devuelve false si no existe ese archivo.</summary>
        public bool CargarPlan(int escenario, string clave)
        {
            PlanResult plan = RepositorioPlanes.Plan(escenario, clave);
            if (plan == null)
            {
                Debug.LogWarning("[ADA] No existe Resources/" + RepositorioPlanes.Ruta(escenario, clave) + ".json");
                return false;
            }
            PlanActual = plan;
            ResumenActual = RepositorioPlanes.Escenario(escenario);
            EscenarioActual = escenario;
            ClaveActual = clave;
            if (OnPlanCargado != null) OnPlanCargado(plan, ResumenActual);
            return true;
        }

        /// <summary>
        /// Suscribe y, si ya hay un plan cargado, llama enseguida al manejador
        /// (útil para objetos que se crean después del primer CargarPlan).
        /// </summary>
        public void Suscribir(Action<PlanResult, ResumenEscenario> manejador)
        {
            OnPlanCargado += manejador;
            if (PlanActual != null) manejador(PlanActual, ResumenActual);
        }

        public void Desuscribir(Action<PlanResult, ResumenEscenario> manejador)
        {
            OnPlanCargado -= manejador;
        }

        /// <summary>Busca el controlador aunque todavía no haya pasado su Awake.</summary>
        public static ControladorPlan Buscar()
        {
            if (Instancia != null) return Instancia;
            return FindFirstObjectByType<ControladorPlan>();
        }
    }
}
