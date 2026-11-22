using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Fabrica.Core
{
    // Escribe un JSON por escenario y algoritmo + resumen_comparativo.json.
    // El serializador se inyecta: en Unity se usa JsonUtility; en consola, System.Text.Json.
    // Así Core no depende de UnityEngine.
    public static class ExportadorJson
    {
        public static List<string> Exportar(List<ResultadoEscenario> resultados, ResumenComparativo resumen,
                                            string carpeta, Func<object, string> serializar)
        {
            Directory.CreateDirectory(carpeta);
            var archivos = new List<string>();
            var utf8 = new UTF8Encoding(false);

            foreach (var res in resultados)
            {
                foreach (var e in res.planes)
                {
                    string ruta = Path.Combine(carpeta, "escenario" + res.escenario.numero + "_" + e.clave + ".json");
                    File.WriteAllText(ruta, serializar(e.plan), utf8);
                    archivos.Add(ruta);
                }
            }

            string rutaResumen = Path.Combine(carpeta, "resumen_comparativo.json");
            File.WriteAllText(rutaResumen, serializar(resumen), utf8);
            archivos.Add(rutaResumen);
            return archivos;
        }
    }
}
