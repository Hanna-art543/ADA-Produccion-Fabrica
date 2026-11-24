using System;
using System.Collections;
using System.IO;
using Fabrica.Core;
using UnityEngine;
using UnityEngine.Networking;

// Lee los JSON que exporta FabricaRunner desde StreamingAssets/Resultados.
//  - Editor / Windows / Mac / Linux: File.ReadAllText.
//  - WebGL / Android (rutas con "://"): UnityWebRequest. Por eso la carga es una corrutina.
public static class CargadorResultados
{
    public const string Carpeta = "Resultados";

    // Sufijos de archivo, en el mismo orden que la tabla del informe.
    public static readonly string[] Claves = { "greedy", "dc", "pd", "int", "random", "bb" };

    public static string NombreMetodo(string clave)
    {
        switch (clave)
        {
            case "greedy": return "Goloso (LPT)";
            case "dc": return "Divide y Vencerás";
            case "pd": return "Prog. Dinámica";
            case "int": return "Integrado (D&V + Goloso + PD)";
            case "random": return "Aleatorio";
            case "bb": return "Branch & Bound";
            default: return clave;
        }
    }

    // Branch & Bound solo existe en el escenario pequeño.
    public static bool Existe(int escenario, string clave)
    {
        return !(clave == "bb" && escenario != 1);
    }

    public static string NombreArchivo(int escenario, string clave)
    {
        return "escenario" + escenario + "_" + clave + ".json";
    }

    public static string RutaArchivo(string nombre)
    {
        return Application.streamingAssetsPath + "/" + Carpeta + "/" + nombre;
    }

    // Lee un archivo de texto de StreamingAssets/Resultados.
    public static IEnumerator LeerTexto(string nombre, Action<string> alTerminar, Action<string> alFallar)
    {
        string ruta = RutaArchivo(nombre);

        if (ruta.Contains("://"))
        {
            using (UnityWebRequest req = UnityWebRequest.Get(ruta))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (alFallar != null) alFallar("No se pudo leer " + ruta + ": " + req.error);
                    yield break;
                }
                if (alTerminar != null) alTerminar(QuitarBom(req.downloadHandler.text));
            }
        }
        else
        {
            if (!File.Exists(ruta))
            {
                if (alFallar != null)
                    alFallar("No existe " + ruta + ". Ejecuta FabricaRunner o el menú Fabrica > Persona 3 > Copiar JSON.");
                yield break;
            }
            string texto = null;
            string error = null;
            try { texto = File.ReadAllText(ruta); }
            catch (Exception ex) { error = ex.Message; }

            if (texto == null)
            {
                if (alFallar != null) alFallar("Error leyendo " + ruta + ": " + error);
                yield break;
            }
            if (alTerminar != null) alTerminar(QuitarBom(texto));
        }
    }

    // Carga escenarioK_<clave>.json y lo convierte a PlanResult.
    public static IEnumerator CargarPlan(int escenario, string clave, Action<PlanResult> alTerminar, Action<string> alFallar)
    {
        string json = null;
        string error = null;
        yield return LeerTexto(NombreArchivo(escenario, clave), t => json = t, e => error = e);

        if (json == null)
        {
            if (alFallar != null) alFallar(error ?? "No se pudo leer el plan.");
            yield break;
        }

        PlanResult plan = null;
        string errorJson = null;
        try { plan = JsonUtility.FromJson<PlanResult>(json); }
        catch (Exception ex) { errorJson = ex.Message; }

        if (plan == null)
        {
            if (alFallar != null) alFallar("JSON inválido en " + NombreArchivo(escenario, clave) + ": " + errorJson);
            yield break;
        }
        if (plan.asignaciones == null) plan.asignaciones = new System.Collections.Generic.List<Assignment>();
        if (alTerminar != null) alTerminar(plan);
    }

    // Carga resumen_comparativo.json (lo usará la Persona 4 para el gráfico de barras).
    public static IEnumerator CargarResumen(Action<ResumenComparativo> alTerminar, Action<string> alFallar)
    {
        string json = null;
        string error = null;
        yield return LeerTexto("resumen_comparativo.json", t => json = t, e => error = e);

        if (json == null)
        {
            if (alFallar != null) alFallar(error ?? "No se pudo leer el resumen.");
            yield break;
        }

        ResumenComparativo resumen = null;
        string errorJson = null;
        try { resumen = JsonUtility.FromJson<ResumenComparativo>(json); }
        catch (Exception ex) { errorJson = ex.Message; }

        if (resumen == null)
        {
            if (alFallar != null) alFallar("JSON inválido en resumen_comparativo.json: " + errorJson);
            yield break;
        }
        if (alTerminar != null) alTerminar(resumen);
    }

    // JsonUtility falla si el texto empieza con el BOM de UTF-8.
    private static string QuitarBom(string texto)
    {
        return texto == null ? null : texto.TrimStart('\uFEFF');
    }
}