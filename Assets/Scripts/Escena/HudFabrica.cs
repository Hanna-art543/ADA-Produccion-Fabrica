using Fabrica.Core;
using UnityEngine;

// PERSONA 3 - Controles mínimos para probar la escena sin la UI de la Persona 4:
// Play / Pausa / Reiniciar, barra de tiempo, velocidad y selector de escenario y método.
// Usa IMGUI (OnGUI), así que no necesita Canvas. Cuando se integre la UI de la Persona 4,
// basta con desmarcar "visible" o quitar este componente.
public class HudFabrica : MonoBehaviour
{
    public VisualizadorFabrica visualizador;
    public bool visible = true;
    public bool mostrarSelectorPlan = true;

    private static readonly float[] Preajustes = { 0.5f, 1f, 2f, 4f, 8f };

    private void Awake()
    {
        if (visualizador == null) visualizador = GetComponent<VisualizadorFabrica>();
    }

    private void OnGUI()
    {
        if (!visible || visualizador == null) return;

        float k = Mathf.Clamp(Screen.height / 720f, 1f, 2.5f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
        float W = Screen.width / k;
        float H = Screen.height / k;
        float ancho = Mathf.Min(620f, W - 20f);
        float alto = mostrarSelectorPlan ? 176f : 112f;

        GUILayout.BeginArea(new Rect((W - ancho) / 2f, H - alto - 8f, ancho, alto), GUI.skin.box);

        // Fila 1: control de reproducción
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(visualizador.Reproduciendo ? "Pausa" : "Play")) visualizador.AlternarPausa();
        if (GUILayout.Button("Reiniciar")) visualizador.Reiniciar();
        GUILayout.Label(visualizador.Plan != null ? visualizador.Plan.algoritmo + " - " + visualizador.Plan.escenario : "Sin plan");
        GUILayout.EndHorizontal();

        // Fila 2: barra de tiempo
        if (visualizador.HayPlan)
        {
            GUI.changed = false;
            float nuevo = GUILayout.HorizontalSlider(visualizador.Tiempo, visualizador.TiempoInicio, visualizador.TiempoFin);
            if (GUI.changed) visualizador.FijarTiempo(nuevo);
        }
        else
        {
            GUILayout.Space(18f);
        }

        // Fila 3: velocidad
        GUILayout.BeginHorizontal();
        GUILayout.Label("Velocidad " + visualizador.Velocidad.ToString("0.##") + "x", GUILayout.Width(110f));
        GUI.changed = false;
        float vel = GUILayout.HorizontalSlider(visualizador.Velocidad, 0.25f, 8f, GUILayout.Width(150f));
        if (GUI.changed) visualizador.FijarVelocidad(vel);
        foreach (float p in Preajustes)
            if (GUILayout.Button(p.ToString("0.#") + "x", GUILayout.Width(38f))) visualizador.FijarVelocidad(p);
        GUILayout.EndHorizontal();

        // Fila 4-5: escenario y método (carga el JSON y llama a CargarPlan)
        if (mostrarSelectorPlan)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Escenario", GUILayout.Width(70f));
            for (int e = 1; e <= 3; e++)
                if (GUILayout.Button(e.ToString())) visualizador.CargarEscenario(e, MetodoSeguro(e));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (string clave in CargadorResultados.Claves)
            {
                if (!CargadorResultados.Existe(visualizador.EscenarioActual, clave)) continue;
                if (GUILayout.Button(CargadorResultados.NombreMetodo(clave)))
                    visualizador.CargarEscenario(Mathf.Max(1, visualizador.EscenarioActual), clave);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    // B&B solo existe en el escenario 1; si estaba elegido y se cambia de escenario, se usa el goloso.
    private string MetodoSeguro(int escenario)
    {
        string m = string.IsNullOrEmpty(visualizador.MetodoActual) ? "greedy" : visualizador.MetodoActual;
        return CargadorResultados.Existe(escenario, m) ? m : "greedy";
    }
}