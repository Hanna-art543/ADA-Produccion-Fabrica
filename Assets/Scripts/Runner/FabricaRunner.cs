using System.IO;
using Fabrica.Core;
using UnityEngine;

// Runner delgado: ejecuta el experimento, imprime la tabla y exporta los JSON a Resultados/.
public class FabricaRunner : MonoBehaviour
{
    public ConfigExperimento config = new ConfigExperimento();
    public bool ejecutarAlIniciar = true;

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
    }
}
