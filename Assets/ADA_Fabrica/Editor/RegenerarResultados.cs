using System.IO;
using Fabrica.Core;
using UnityEditor;
using UnityEngine;

public static class RegenerarResultados
{
    [MenuItem("ADA Fabrica/Regenerar resultados (con integrados)")]
    public static void Ejecutar()
    {
        var cfg = new ConfigExperimento();
        var resultados = Experimento.Ejecutar(cfg);
        var resumen = Experimento.Resumir(resultados, cfg.semilla);
        foreach (var r in resumen.escenarios) Debug.Log(Comparador.Tabla(r));

        string carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Resultados"));
        var archivos = ExportadorJson.Exportar(resultados, resumen, carpeta, o => JsonUtility.ToJson(o, true));
        Debug.Log("Se exportaron " + archivos.Count + " archivos JSON a " + carpeta);
        EditorUtility.DisplayDialog("ADA Fabrica",
                                    "Listo: " + archivos.Count + " archivos en " + carpeta +
                                    "\n\nAhora ejecuta el menú 'Importar Resultados' para copiarlos a Unity.", "OK");
    }
}
