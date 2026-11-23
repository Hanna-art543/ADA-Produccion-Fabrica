using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fabrica.UI.EditorADA
{
    /// <summary>
    /// Configuración de WebGL para GitHub Pages. Pages NO envía "Content-Encoding: br",
    /// así que no se usa Brotli: compresión desactivada (lo más seguro) o Gzip + Decompression Fallback.
    /// </summary>
    public static class WebGLGitHubPages
    {
        [MenuItem("ADA Fabrica/WebGL: aplicar configuración para GitHub Pages (sin compresión)", false, 20)]
        public static void SinCompresion()
        {
            Aplicar(WebGLCompressionFormat.Disabled, false);
        }

        [MenuItem("ADA Fabrica/WebGL: aplicar configuración para GitHub Pages (Gzip + fallback)", false, 21)]
        public static void ConGzip()
        {
            Aplicar(WebGLCompressionFormat.Gzip, true);
        }

        private static void Aplicar(WebGLCompressionFormat formato, bool fallback)
        {
            PlayerSettings.WebGL.compressionFormat = formato;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
            Debug.Log("[ADA] WebGL listo para GitHub Pages: compresión " + formato + (fallback ? " + Decompression Fallback" : "") +
                      ", nombres sin hash, data caching, run in background.");
        }

        [MenuItem("ADA Fabrica/WebGL: build a la carpeta docs/ (GitHub Pages)", false, 22)]
        public static void Build()
        {
            string escena = ConstructorUI_ADA.RutaEscena;
            if (!File.Exists(escena))
            {
                EditorUtility.DisplayDialog("ADA Fabrica", "Primero construye la escena (ADA Fabrica → 2) o añade tu escena final a Build Settings.", "OK");
                return;
            }
            string inicial = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string destino = EditorUtility.SaveFolderPanel("Carpeta docs/ del repositorio (GitHub Pages)", inicial, "docs");
            if (string.IsNullOrEmpty(destino)) return;
            if (Path.GetFileName(destino) != "docs" &&
                !EditorUtility.DisplayDialog("ADA Fabrica", "La carpeta no se llama \"docs\". GitHub Pages publica desde /docs o desde la raíz. ¿Seguir?", "Seguir", "Cancelar"))
                return;

            if (PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Brotli) SinCompresion();

            // Escenas: las marcadas en Build Settings; si no hay ninguna, la de prueba
            var escenas = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) escenas.Add(s.path);
            if (escenas.Count == 0) escenas.Add(escena);

            BuildReport rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = escenas.ToArray(),
                locationPathName = destino,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (rep != null && rep.summary.result == BuildResult.Succeeded)
            {
                File.WriteAllText(Path.Combine(destino, ".nojekyll"), "");
                EditorUtility.DisplayDialog("ADA Fabrica", "Build WebGL listo en:\n" + destino +
                    "\n\nSe creó .nojekyll. Haz commit de docs/ y en GitHub: Settings → Pages → Branch: main, carpeta /docs.", "OK");
                EditorUtility.RevealInFinder(Path.Combine(destino, "index.html"));
            }
            else
            {
                EditorUtility.DisplayDialog("ADA Fabrica", "El build falló. Revisa la consola.", "OK");
            }
        }
    }
}
