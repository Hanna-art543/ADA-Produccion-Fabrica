using UnityEngine;

namespace Fabrica.UI
{
    /// <summary>
    /// Reloj de la simulación. La UI (cursor del Gantt) SOLO depende de esta interfaz.
    /// La escena 3D de la Persona 3 implementa esto con su propio reloj.
    /// </summary>
    public interface IRelojSimulacion
    {
        /// <summary>Tiempo simulado actual (mismas unidades que inicio/fin de las asignaciones).</summary>
        double Tiempo { get; }
        /// <summary>Duración total del plan actual.</summary>
        double Makespan { get; }
        bool Reproduciendo { get; }
    }

    /// <summary>Opcional: si el reloj además implementa esto, la barra de controles de la UI lo maneja.</summary>
    public interface IRelojControlable : IRelojSimulacion
    {
        void Reproducir();
        void Pausar();
        void Reiniciar();
        /// <summary>Multiplicador de velocidad (x0.5 … x8).</summary>
        float Velocidad { get; set; }
    }

    /// <summary>Opcional: permite que un clic en el Gantt mueva el reloj a ese tiempo.</summary>
    public interface IRelojBuscable
    {
        void IrA(double t);
    }

    /// <summary>Busca en la escena un componente que implemente la interfaz de reloj.</summary>
    public static class BuscadorReloj
    {
        public static T Buscar<T>(MonoBehaviour preferido = null) where T : class
        {
            T r = preferido as T;
            if (r != null) return r;
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                T x = mb as T;
                if (x != null && mb.isActiveAndEnabled) return x;
            }
            return null;
        }
    }
}
