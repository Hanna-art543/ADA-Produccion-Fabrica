using System.Collections.Generic;
using System.IO;
using System.Text;
using Fabrica.Core;
using Fabrica.Escena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fabrica.UI.EditorADA
{
    /// <summary>
    /// Escena FINAL: la fábrica de la Persona 3 (VisualizadorFabrica, que es el reloj único) + el prefab ADA_UI
    /// (ControladorPlan, Gantt, gráfico, Comparar, controles). Es la única escena del build.
    /// Incluye un verificador de la escena y de los datos reales.
    /// </summary>
    public static class EscenaFinalADA
    {
        public const string RutaFinal = ConstructorUI_ADA.Raiz + "/Scenes/ADA_Fabrica.unity";
        public const string CarpetaModelos = ConstructorUI_ADA.Raiz + "/Modelos";

        private static GameObject Modelo(string nombre)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaModelos + "/" + nombre + ".fbx");
        }

        [MenuItem("ADA Fabrica/3. Construir escena FINAL ADA_Fabrica (única del build)", false, 3)]
        public static void ConstruirMenu()
        {
            Construir(RutaFinal, true, null, true);
        }

        /// <summary>Arma la escena final (fábrica 3D con operarios + UI) y la deja como única escena del build.</summary>
        public static void Construir(string ruta, bool gatitos, GameObject prefabGato, bool unicaEnBuild)
        {
            if (!ConstructorUI_ADA.ChequeosPrevios()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameObject prefab = ConstructorUI_ADA.ConstruirPrefab();

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            cam.fieldOfView = 40f;
            var luz = new GameObject("Directional Light", typeof(Light));
            Light l = luz.GetComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
            luz.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            // luz de relleno suave desde el frente (sin sombras)
            var relleno = new GameObject("Luz de relleno", typeof(Light));
            Light lr = relleno.GetComponent<Light>();
            lr.type = LightType.Directional;
            lr.intensity = 0.35f;
            relleno.transform.rotation = Quaternion.Euler(20f, 160f, 0f);

            // Nombre completo: en el repo también existe la clase global VisualizadorFabrica de la primera escena (Assets/Scripts/Escena).
            var fab = new GameObject("Fabrica", typeof(global::Fabrica.Escena.VisualizadorFabrica));
            global::Fabrica.Escena.VisualizadorFabrica vis = fab.GetComponent<global::Fabrica.Escena.VisualizadorFabrica>();
            vis.camara = cam;
            vis.cargarAlIniciar = false;          // los datos los da ControladorPlan
            vis.mostrarGatitos = gatitos;
            vis.alturaGato = 0.9f;
            GameObject obrero = Modelo("GatoObrero");
            vis.prefabGato = obrero != null ? obrero : prefabGato;
            vis.modeloMaquina = Modelo("MaquinaFabrica");
            vis.modeloCinta = Modelo("CintaTransportadora");
            vis.modelosPieza = new[] { Modelo("Pieza0"), Modelo("Pieza1"), Modelo("Pieza2"), Modelo("Pieza3") };
            if (gatitos || vis.modeloMaquina != null)
            {
                AmbienteFabrica amb = fab.AddComponent<AmbienteFabrica>();
                foreach (string n in new[] { "MuroVentana", "Columna", "Estanteria", "Palet", "Tanque", "CajaCarton", "MesaControl", "Lampara", "Montacargas", "Carrito", "GatoObrero" })
                    amb.modelos.Add(new AmbienteFabrica.Modelo { nombre = n, prefab = n == "GatoObrero" ? vis.prefabGato : Modelo(n) });
            }
            int faltan = 0;
            foreach (string n in new[] { "MaquinaFabrica", "CintaTransportadora", "Pieza0", "GatoObrero", "Estanteria" }) if (Modelo(n) == null) faltan++;
            if (faltan > 0) Debug.LogWarning("[ADA] Faltan modelos en " + CarpetaModelos + ": se usan primitivas en su lugar.");

            var ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ui.GetComponent<AreaLibreCamara>().camara = cam;

            ConstructorUI_ADA.Carpeta(Path.GetDirectoryName(ruta).Replace('\\', '/'));
            EditorSceneManager.SaveScene(escena, ruta);
            if (unicaEnBuild) ConstructorUI_ADA.UnicaEnBuild(ruta);
            Debug.Log("[ADA] Escena final lista: " + ruta + (unicaEnBuild ? " (única en Build Settings)" : "") + ". Dale Play.");
            Verificar(unicaEnBuild ? ruta : null);
        }

        // ------------------------------------------------------------------ verificación

        [MenuItem("ADA Fabrica/4. Verificar escena abierta y datos reales", false, 4)]
        public static void VerificarMenu()
        {
            Verificar(RutaFinal);
        }

        /// <summary>rutaUnicaEnBuild = null → no revisa Build Settings.</summary>
        public static void Verificar(string rutaUnicaEnBuild)
        {
            var sb = new StringBuilder();
            int fallos = 0;
            System.Action<bool, string> ok = (c, m) => { sb.AppendLine((c ? "[OK] " : "[X]  ") + m); if (!c) fallos++; };

            // ---- escena
            var relojes = new List<string>();
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb is IRelojSimulacion) relojes.Add(mb.GetType().Name);
            ok(Object.FindObjectsByType<ControladorPlan>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Un solo ControladorPlan");
            ok(relojes.Count == 1, "Un solo reloj en la escena (" + string.Join(", ", relojes.ToArray()) + ")");
            ok(Object.FindObjectsByType<RelojSimple>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Sin RelojSimple (el reloj es VisualizadorFabrica)");
            bool runner = false;
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && mb.GetType().Name == "FabricaRunner") runner = true;
            ok(!runner, "FabricaRunner no está en la escena");
            bool hud = false;
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && mb.GetType().Name == "HudFabrica" && mb.enabled) hud = true;
            ok(!hud, "Un solo juego de controles (sin HudFabrica activo)");
            if (rutaUnicaEnBuild != null)
            {
                EditorBuildSettingsScene[] enBuild = EditorBuildSettings.scenes;
                ok(enBuild.Length == 1 && enBuild[0].path == rutaUnicaEnBuild, "Build Settings: solo " + rutaUnicaEnBuild);
            }

            // ---- datos reales
            RepositorioPlanes.LimpiarCache();
            ok(RepositorioPlanes.Resumen != null, "resumen_comparativo.json se lee");
            int planes = 0;
            for (int e = 1; e <= 3; e++)
                foreach (string c in RepositorioPlanes.ClavesDisponibles(e))
                {
                    PlanResult p = RepositorioPlanes.Plan(e, c);
                    if (p != null) planes++;
                    var m = new TiempoPiezas(p);
                    bool todasSalida = m.PiezasTerminadas(m.Makespan) == m.CantidadPiezas;
                    double eFin = m.Energia(m.Makespan);
                    if (p.asignaciones.Count > 0)
                        ok(todasSalida && System.Math.Abs(eFin - p.energiaTotal) < 0.01,
                           "Esc " + e + " " + c + ": " + p.asignaciones.Count + " tareas, en t=makespan " + m.PiezasTerminadas(m.Makespan) + "/" + m.CantidadPiezas +
                           " piezas en la salida, energía " + CalculosFabrica.N(eFin, 2) + " = " + CalculosFabrica.N(p.energiaTotal, 2));
                }
            // greedy, dc, pd, int, random en los 3 escenarios + bb en el 1 = 3·5 + 1 = 16
            ok(planes == 16, "16 planes cargan (" + planes + ")");

            PlanResult g1 = RepositorioPlanes.Plan(1, "greedy");
            ok(g1 != null && System.Math.Abs(g1.makespan - 167.8) < 0.05 && System.Math.Abs(g1.energiaTotal - 1040.5) < 0.05 &&
               System.Math.Abs(g1.costoTotal - 271.85) < 0.005 && RepositorioPlanes.CantidadMaquinas(1) == 3,
               "Esc 1 Goloso: makespan " + (g1 != null ? CalculosFabrica.N(g1.makespan, 2) : "?") + ", energía " +
               (g1 != null ? CalculosFabrica.N(g1.energiaTotal, 1) + " / " + CalculosFabrica.N(g1.energiaMax, 1) : "?") +
               ", costo " + (g1 != null ? CalculosFabrica.N(g1.costoTotal, 2) : "?") + ", " + RepositorioPlanes.CantidadMaquinas(1) + " máquinas");
            ok(MaquinasVacias(2, "pd") == "1" && RepositorioPlanes.CantidadMaquinas(2) == 5, "Esc 2 PD: 5 máquinas, vacías: " + MaquinasVacias(2, "pd"));
            ok(MaquinasVacias(3, "pd") == "0,7" && RepositorioPlanes.CantidadMaquinas(3) == 8, "Esc 3 PD: 8 máquinas, vacías: " + MaquinasVacias(3, "pd"));
            PlanResult r3 = RepositorioPlanes.Plan(3, "random");
            var txt = CalculosFabrica.TextoComparacion("random", r3, r3, -1, "");
            ok(r3 != null && r3.asignaciones.Count == 0 && txt.Count > 0 && txt[0] == CalculosFabrica.MensajeSinFactibleAleatorio,
               "Esc 3 Aleatorio: vacío, Comparar muestra el mensaje de sin solución");

            // ---- Comparar vs resumen
            for (int e = 1; e <= 3; e++)
            {
                ResumenEscenario r = RepositorioPlanes.Escenario(e);
                FilaComparativa fa = CalculosFabrica.FilaDe(r, "random");
                if (fa == null || fa.sinSolucion) continue;
                foreach (string c in RepositorioPlanes.ClavesDisponibles(e))
                {
                    FilaComparativa fm = CalculosFabrica.FilaDe(r, c);
                    PlanResult pm = RepositorioPlanes.Plan(e, c);
                    PlanResult pa = RepositorioPlanes.Plan(e, "random");
                    if (fm == null || pm == null) continue;
                    double ui = CalculosFabrica.PorcentajeMejora(pa.costoTotal, pm.costoTotal);
                    double res = CalculosFabrica.PorcentajeMejora(fa.costo, fm.costo);
                    ok(System.Math.Abs(ui - res) < 0.01, "Comparar esc " + e + " " + c + ": " + CalculosFabrica.Signo(ui, 2) + " % (resumen " + CalculosFabrica.Signo(res, 2) + " %)");
                }
            }

            string titulo = fallos == 0 ? "Verificación ADA: TODO OK" : "Verificación ADA: " + fallos + " punto(s) a revisar";
            Debug.Log("[ADA] " + titulo + "\n" + sb);
            EditorUtility.DisplayDialog("ADA Fabrica", titulo + "\n\n(el detalle está en la consola)", "OK");
        }

        private static string MaquinasVacias(int esc, string clave)
        {
            PlanResult p = RepositorioPlanes.Plan(esc, clave);
            if (p == null) return "?";
            var usadas = new HashSet<int>();
            foreach (Assignment a in p.asignaciones) usadas.Add(a.maquinaId);
            var vacias = new List<string>();
            int b = RepositorioPlanes.BaseIdsMaquina(esc);
            for (int i = 0; i < RepositorioPlanes.CantidadMaquinas(esc); i++) if (!usadas.Contains(b + i)) vacias.Add((b + i).ToString());
            return string.Join(",", vacias.ToArray());
        }
    }
}
