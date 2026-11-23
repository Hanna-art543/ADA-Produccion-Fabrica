using System.IO;
using Fabrica.Core;
using UnityEngine;

// Runner delgado: ejecuta el experimento, imprime la tabla y exporta los JSON a Resultados/.
public class FabricaRunner : MonoBehaviour
{
    public ConfigExperimento config = new ConfigExperimento();
    public bool ejecutarAlIniciar = true;

    // PERSONA 3: copia los JSON a Assets/StreamingAssets/Resultados, que es de donde los lee la escena.
    public bool copiarAStreamingAssets = true;

    private void Start()
    {
        if (ejecutarAlIniciar) Ejecutar();
    }

    [ContextMenu("Ejecutar experimento")]
    public void Ejecutar()
    {
        var resultados = Experimento.Ejecutar(config);
        var resumen = Experimento.Resumir(resultados, config.semilla);

        foreach (var r in resumen.escenarios) Debug.Log(Comparador.Tabla(r));

        // Resultados/ está en la raíz del proyecto (junto a Assets/)
        string carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resultados"));
        var archivos = ExportadorJson.Exportar(resultados, resumen, carpeta, o => JsonUtility.ToJson(o, true));
        Debug.Log("Se exportaron " + archivos.Count + " archivos JSON a " + carpeta);

#if UNITY_EDITOR
        // PERSONA 3: la escena (VisualizadorFabrica) lee los planes desde StreamingAssets/Resultados
        if (copiarAStreamingAssets)
        {
            string destino = Path.Combine(Application.streamingAssetsPath, "Resultados");
            ExportadorJson.Exportar(resultados, resumen, destino, o => JsonUtility.ToJson(o, true));
            Debug.Log("Copia para la escena en " + destino + " (refresca el Project si no la ves)");
        }
#endif
    }
}