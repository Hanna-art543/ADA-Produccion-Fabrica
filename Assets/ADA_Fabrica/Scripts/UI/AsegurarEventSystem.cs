using UnityEngine;
using UnityEngine.EventSystems;

namespace Fabrica.UI
{
    /// <summary>Crea un EventSystem si la escena no tiene (con el módulo de entrada que corresponda).</summary>
    public class AsegurarEventSystem : MonoBehaviour
    {
        private void Awake()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            System.Type modulo = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (modulo != null) go.AddComponent(modulo);
            else go.AddComponent<StandaloneInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
