using System.IO;
using Fabrica.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Fabrica.UI.EditorADA
{
    /// <summary>
    /// Menú "ADA Fabrica": importa los JSON, arma el prefab ADA_UI y la escena de prueba ADA_UI_Test.
    /// Todo el prefab se arma por código para que sea reproducible (y fácil de corregir).
    /// </summary>
    public static class ConstructorUI_ADA
    {
        public const string Raiz = "Assets/ADA_Fabrica";
        public const string RutaPrefab = Raiz + "/Prefabs/ADA_UI.prefab";
        public const string RutaEscena = Raiz + "/Scenes/ADA_UI_Test.unity";
        public const string RutaResultados = Raiz + "/Resources/Resultados";

        // ------------------------------------------------------------------ importar JSON

        [MenuItem("ADA Fabrica/1. Importar Resultados (JSON) desde el repo...", false, 1)]
        public static void ImportarResultados()
        {
            string carpeta = EditorUtility.OpenFolderPanel("Carpeta del repo (o su carpeta Resultados)", "", "");
            if (string.IsNullOrEmpty(carpeta)) return;
            string origen = carpeta;
            if (!File.Exists(Path.Combine(origen, "resumen_comparativo.json")))
                origen = Path.Combine(carpeta, "Resultados");
            if (!File.Exists(Path.Combine(origen, "resumen_comparativo.json")))
            {
                EditorUtility.DisplayDialog("ADA Fabrica", "No encuentro resumen_comparativo.json en esa carpeta ni en su subcarpeta Resultados.", "OK");
                return;
            }
            string destino = Path.GetFullPath(RutaResultados);
            Directory.CreateDirectory(destino);
            int n = 0;
            foreach (string f in Directory.GetFiles(origen, "*.json"))
            {
                File.Copy(f, Path.Combine(destino, Path.GetFileName(f)), true);
                n++;
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("ADA Fabrica", n + " archivos JSON copiados a " + RutaResultados + ".", "OK");
        }

        // ------------------------------------------------------------------ prefab + escena

        [MenuItem("ADA Fabrica/2. Construir prefab ADA_UI y escena ADA_UI_Test", false, 2)]
        public static void Construir()
        {
            if (!ChequeosPrevios()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameObject prefab = ConstruirPrefab();
            ConstruirEscenaPrueba(prefab);
        }

        /// <summary>TMP importado y JSON presentes (si faltan, pregunta).</summary>
        public static bool ChequeosPrevios()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("ADA Fabrica",
                    "Falta importar TextMeshPro (TMP Essential Resources). Se abrirá el importador: pulsa \"Import TMP Essentials\" " +
                    "y luego vuelve a ejecutar este menú.", "OK");
                EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
                return false;
            }
            if (Resources.Load<TextAsset>("Resultados/resumen_comparativo") == null)
            {
                if (!EditorUtility.DisplayDialog("ADA Fabrica",
                    "No están los JSON en " + RutaResultados + ". Puedes construir igual, pero la UI saldrá vacía.\n\n" +
                    "Usa antes \"ADA Fabrica → 1. Importar Resultados\".", "Construir igual", "Cancelar")) return false;
            }
            return true;
        }

        public static GameObject ConstruirPrefab()
        {
            Carpeta(Raiz + "/Prefabs");
            Carpeta(Raiz + "/Scenes");
            GameObject raiz = ArmarUI();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            Object.DestroyImmediate(raiz);
            return prefab;
        }

        private static void ConstruirEscenaPrueba(GameObject prefab)
        {
            // ---- escena de prueba con RelojSimple (no toca escenas ajenas y no va al build)
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            Camera c = cam.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            cam.transform.position = new Vector3(0f, 7f, -11f);
            cam.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            var luz = new GameObject("Directional Light", typeof(Light));
            luz.GetComponent<Light>().type = LightType.Directional;
            luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            MaquetaFabrica();
            new GameObject("RelojSimple (provisional)", typeof(RelojSimple));
            PrefabUtility.InstantiatePrefab(prefab);
            EditorSceneManager.SaveScene(escena, RutaEscena);   // escena de prueba: NO va al build

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ADA] Listo: " + RutaPrefab + " y " + RutaEscena + ". Dale Play.");
        }

        /// <summary>Cajas grises en el centro, solo para ver que la vista 3D queda libre.</summary>
        private static void MaquetaFabrica()
        {
            var grupo = new GameObject("Maqueta 3D (reemplazar por la escena de la Persona 3)");
            GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
            piso.name = "Piso";
            piso.transform.SetParent(grupo.transform);
            piso.transform.localScale = new Vector3(2f, 1f, 1.2f);
            for (int i = 0; i < 3; i++)
            {
                GameObject m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = "Maquina " + (i + 1);
                m.transform.SetParent(grupo.transform);
                m.transform.position = new Vector3(-4f + i * 4f, 0.75f, 1.5f);
                m.transform.localScale = new Vector3(2f, 1.5f, 2f);
            }
        }

        /// <summary>Deja esa escena como la ÚNICA del build.</summary>
        public static void UnicaEnBuild(string ruta)
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ruta, true) };
        }

        public static void Carpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            Carpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        // ------------------------------------------------------------------ armado de la UI

        private static TMP_DefaultControls.Resources recursos;

        private static GameObject ArmarUI()
        {
            recursos = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };

            // ---- Canvas raíz
            var raiz = new GameObject("ADA_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            raiz.layer = 5;
            Canvas canvas = raiz.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler esc = raiz.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(1920f, 1080f);
            esc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            esc.matchWidthOrHeight = 0.5f;
            raiz.AddComponent<ControladorPlan>();
            raiz.AddComponent<AsegurarEventSystem>();
            raiz.AddComponent<AreaLibreCamara>();
            Transform R = raiz.transform;

            // ================================================================ PANEL LATERAL (izquierda)
            const float anchoLateral = 470f;
            Image lateral = UIFabrica.Imagen("PanelLateral", R, Paleta.Fondo, true);
            RectTransform lat = lateral.rectTransform;
            lat.anchorMin = new Vector2(0, 0); lat.anchorMax = new Vector2(0, 1);
            lat.pivot = new Vector2(0, 0.5f);
            lat.offsetMin = new Vector2(0, 0); lat.offsetMax = new Vector2(anchoLateral, 0);

            Texto("Titulo", lat, "ADA · Planificación de la fábrica", 22, Paleta.Texto, FontStyles.Bold, 18, 14, 30);
            Texto("Subtitulo", lat, "Minimizar  α·makespan + β·energía  sin pasar E_max", 13, Paleta.TextoSuave, FontStyles.Normal, 18, 44, 20);

            // ---- tarjeta selección
            RectTransform tSel = Tarjeta("Seleccion", lat, 72, 362);
            Texto("lblEscenario", tSel, "ESCENARIO", 11.5f, Paleta.TextoSuave, FontStyles.Bold, 14, 10, 16);
            TMP_Dropdown ddEsc = Dropdown("DropdownEscenario", tSel, 28);
            Texto("lblMetodo", tSel, "MÉTODO", 11.5f, Paleta.TextoSuave, FontStyles.Bold, 14, 70, 16);
            TMP_Dropdown ddMet = Dropdown("DropdownMetodo", tSel, 88);
            TextMeshProUGUI algoritmo = Texto("Algoritmo", tSel, "—", 19, Paleta.Acento, FontStyles.Bold, 14, 132, 26);
            TextMeshProUGUI detalle = Texto("DetalleEscenario", tSel, "", 12.5f, Paleta.TextoSuave, FontStyles.Normal, 14, 158, 18);
            TextMeshProUGUI metricas = Texto("Metricas", tSel, "", 14.5f, Paleta.Texto, FontStyles.Normal, 14, 180, 128);
            metricas.alignment = TextAlignmentOptions.TopLeft;
            metricas.lineSpacing = 6f;

            Texto("lblBarra", tSel, "energía / E_max", 10.5f, Paleta.TextoSuave, FontStyles.Normal, 14, 308, 14);
            Image fondoBarra = UIFabrica.Imagen("BarraEnergia", tSel, Paleta.Linea);
            UIFabrica.FilaArriba(fondoBarra.rectTransform, 324, 8, 14, 14);
            Image relleno = UIFabrica.Imagen("Relleno", fondoBarra.transform, Paleta.Bien);
            UIFabrica.Anclar(relleno.rectTransform, 0, 0, 0.5f, 1);
            TextMeshProUGUI notas = Texto("Notas", tSel, "", 11, Paleta.TextoSuave, FontStyles.Italic, 14, 336, 24);
            notas.enableWordWrapping = true;
            notas.alignment = TextAlignmentOptions.TopLeft;

            PanelSeleccion ps = tSel.gameObject.AddComponent<PanelSeleccion>();
            ps.escenario = ddEsc; ps.metodo = ddMet; ps.algoritmo = algoritmo; ps.detalleEscenario = detalle;
            ps.metricas = metricas; ps.barraEnergia = relleno.rectTransform; ps.rellenoEnergia = relleno; ps.notas = notas;

            // ---- tarjeta comparar
            RectTransform tCmp = Tarjeta("Comparar", lat, 444, 152);
            Button bCmp = Boton("BotonComparar", tCmp, "Comparar con Aleatorio", 14, 10, -1, 36, Paleta.Acento, new Color(0.08f, 0.09f, 0.11f));
            TextMeshProUGUI resCmp = Texto("Resultado", tCmp, "", 13.5f, Paleta.Texto, FontStyles.Normal, 14, 54, 92);
            resCmp.enableWordWrapping = true;
            resCmp.alignment = TextAlignmentOptions.TopLeft;
            PanelComparar pc = tCmp.gameObject.AddComponent<PanelComparar>();
            pc.boton = bCmp; pc.resultado = resCmp;

            // ---- tarjeta gráfico comparativo (hasta abajo)
            Image tGraf = UIFabrica.Imagen("Tarjeta Grafico", lat, Paleta.Tarjeta);
            RectTransform gr = tGraf.rectTransform;
            gr.anchorMin = new Vector2(0, 0); gr.anchorMax = new Vector2(1, 1);
            gr.offsetMin = new Vector2(12, 12); gr.offsetMax = new Vector2(-12, -606);
            Texto("TituloGrafico", gr, "Comparación del escenario", 15, Paleta.Texto, FontStyles.Bold, 14, 8, 22);
            TextMeshProUGUI refTxt = Texto("Referencia", gr, "", 12, Paleta.Texto, FontStyles.Normal, 14, 30, 36);
            refTxt.alignment = TextAlignmentOptions.TopLeft;
            RectTransform contenido = UIFabrica.Nuevo("Contenido", gr);
            contenido.anchorMin = new Vector2(0, 0); contenido.anchorMax = new Vector2(1, 1);
            contenido.offsetMin = new Vector2(12, 8); contenido.offsetMax = new Vector2(-12, -72);
            GraficoComparativo gc = gr.gameObject.AddComponent<GraficoComparativo>();
            gc.contenido = contenido; gc.referencia = refTxt;

            // ================================================================ PANEL INFERIOR (Gantt)
            const float altoInferior = 330f;
            Image inferior = UIFabrica.Imagen("PanelInferior", R, Paleta.Fondo, true);
            RectTransform inf = inferior.rectTransform;
            inf.anchorMin = new Vector2(0, 0); inf.anchorMax = new Vector2(1, 0);
            inf.pivot = new Vector2(0.5f, 0);
            inf.offsetMin = new Vector2(anchoLateral + 8f, 8f); inf.offsetMax = new Vector2(-8f, 8f + altoInferior);

            TextMeshProUGUI tituloGantt = Texto("TituloGantt", inf, "Gantt", 14, Paleta.Texto, FontStyles.Normal, 14, 8, 24);
            tituloGantt.rectTransform.offsetMax = new Vector2(-860f, tituloGantt.rectTransform.offsetMax.y);

            // controles del reloj (provisionales)
            RectTransform ctr = UIFabrica.Nuevo("ControlesReloj", inf);
            ctr.anchorMin = ctr.anchorMax = new Vector2(1, 1); ctr.pivot = new Vector2(1, 1);
            ctr.anchoredPosition = new Vector2(-430f, -6f); ctr.sizeDelta = new Vector2(420f, 28f);
            RectTransform grupoBotones = UIFabrica.Anclar(UIFabrica.Nuevo("Botones", ctr), 0, 0, 1, 1);
            Button bPlay = BotonFijo("Play", grupoBotones, "Pausa", 0, 70);
            Button bReset = BotonFijo("Reiniciar", grupoBotones, "Reiniciar", 74, 80);
            Button bMenos = BotonFijo("Menos", grupoBotones, "-", 160, 28);
            TextMeshProUGUI tVel = UIFabrica.Texto("Velocidad", grupoBotones, "x1", 13, Paleta.Texto, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFabrica.ArribaIzq(tVel.rectTransform, 190, 0, 36, 28);
            Button bMas = BotonFijo("Mas", grupoBotones, "+", 228, 28);
            TextMeshProUGUI tTiempo = UIFabrica.Texto("Tiempo", ctr, "", 13, Paleta.Acento, TextAlignmentOptions.MidlineLeft);
            UIFabrica.ArribaIzq(tTiempo.rectTransform, 264, 0, 160, 28);
            ControlesReloj cr = ctr.gameObject.AddComponent<ControlesReloj>();
            cr.reproducir = bPlay; cr.textoReproducir = bPlay.GetComponentInChildren<TMP_Text>(true);
            cr.reiniciar = bReset; cr.masLento = bMenos; cr.masRapido = bMas;
            cr.textoVelocidad = tVel; cr.textoTiempo = tTiempo; cr.grupoBotones = grupoBotones.gameObject;

            // leyenda de etapas
            RectTransform ley = UIFabrica.Nuevo("Leyenda", inf);
            ley.anchorMin = ley.anchorMax = new Vector2(1, 1); ley.pivot = new Vector2(1, 1);
            ley.anchoredPosition = new Vector2(-14f, -6f); ley.sizeDelta = new Vector2(410f, 28f);
            ItemLeyenda(ley, 0, Paleta.Corte, "Corte", 14f);
            ItemLeyenda(ley, 82, Paleta.Ensamblaje, "Ensamblaje", 14f);
            ItemLeyenda(ley, 196, Paleta.Empaquetado, "Empaquetado", 14f);
            ItemLeyenda(ley, 320, Paleta.Acento, "tiempo", 3f);

            // zona del Gantt
            RectTransform filas = UIFabrica.Nuevo("Maquinas", inf);
            filas.anchorMin = new Vector2(0, 0); filas.anchorMax = new Vector2(0, 1);
            filas.pivot = new Vector2(0, 0.5f);
            filas.offsetMin = new Vector2(8f, 30f); filas.offsetMax = new Vector2(96f, -42f);

            Image areaImg = UIFabrica.Imagen("AreaGantt", inf, new Color(0, 0, 0, 0.15f), true);
            RectTransform area = areaImg.rectTransform;
            area.anchorMin = new Vector2(0, 0); area.anchorMax = new Vector2(1, 1);
            area.offsetMin = new Vector2(100f, 30f); area.offsetMax = new Vector2(-24f, -42f);

            RectTransform eje = UIFabrica.Nuevo("EjeX", inf);
            eje.anchorMin = new Vector2(0, 0); eje.anchorMax = new Vector2(1, 0);
            eje.pivot = new Vector2(0.5f, 0);
            eje.offsetMin = new Vector2(100f, 6f); eje.offsetMax = new Vector2(-24f, 28f);

            Image cursor = UIFabrica.Imagen("Cursor", area, Paleta.Acento);
            RectTransform crt = cursor.rectTransform;
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.offsetMin = new Vector2(-1f, -4f); crt.offsetMax = new Vector2(1f, 4f);
            TextMeshProUGUI cursorTxt = UIFabrica.Texto("t", crt, "t = 0", 11.5f, Paleta.Acento, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            RectTransform ctt = cursorTxt.rectTransform;
            ctt.anchorMin = ctt.anchorMax = new Vector2(0.5f, 1); ctt.pivot = new Vector2(0, 0);
            ctt.anchoredPosition = new Vector2(3f, 0f); ctt.sizeDelta = new Vector2(90f, 16f);

            TextMeshProUGUI msg = UIFabrica.Texto("Mensaje", area, "", 17, Paleta.Acento, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFabrica.Anclar(msg.rectTransform, 0, 0, 1, 1, 20, 10, 20, 10);
            msg.enableWordWrapping = true;
            msg.gameObject.SetActive(false);

            // ================================================================ TOOLTIP (encima de todo)
            Image tip = UIFabrica.Imagen("Tooltip", R, new Color(0.04f, 0.05f, 0.07f, 0.96f));
            RectTransform tipRt = tip.rectTransform;
            tipRt.sizeDelta = new Vector2(260f, 90f);
            Image borde = UIFabrica.Imagen("Borde", tip.transform, Paleta.Acento);
            UIFabrica.Anclar(borde.rectTransform, 0, 0, 0, 1, 0, 0, -3f, 0);
            TextMeshProUGUI tipTxt = UIFabrica.Texto("Texto", tip.transform, "", 13.5f, Paleta.Texto, TextAlignmentOptions.TopLeft);
            UIFabrica.Anclar(tipTxt.rectTransform, 0, 0, 1, 1, 12, 6, 8, 7);
            tipTxt.lineSpacing = 4f;
            TooltipUI tooltip = raiz.AddComponent<TooltipUI>();
            tooltip.panel = tipRt; tooltip.texto = tipTxt;
            tip.gameObject.SetActive(false);

            GanttChart gantt = area.gameObject.AddComponent<GanttChart>();
            gantt.area = area; gantt.columnaFilas = filas; gantt.ejeX = eje; gantt.titulo = tituloGantt;
            gantt.mensaje = msg; gantt.cursor = crt; gantt.cursorTexto = cursorTxt; gantt.tooltip = tooltip;

            return raiz;
        }

        // ------------------------------------------------------------------ piezas

        private static TextMeshProUGUI Texto(string nombre, RectTransform padre, string txt, float tam, Color color,
                                             FontStyles estilo, float x, float y, float alto)
        {
            TextMeshProUGUI t = UIFabrica.Texto(nombre, padre, txt, tam, color, TextAlignmentOptions.MidlineLeft, estilo);
            UIFabrica.FilaArriba(t.rectTransform, y, alto, x, x);
            return t;
        }

        private static RectTransform Tarjeta(string nombre, RectTransform padre, float y, float alto)
        {
            Image img = UIFabrica.Imagen("Tarjeta " + nombre, padre, Paleta.Tarjeta);
            return UIFabrica.FilaArriba(img.rectTransform, y, alto, 12, 12);
        }

        private static TMP_Dropdown Dropdown(string nombre, RectTransform padre, float y)
        {
            GameObject go = TMP_DefaultControls.CreateDropdown(recursos);
            go.name = nombre;
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            UIFabrica.FilaArriba(rt, y, 34, 14, 14);
            TMP_Dropdown dd = go.GetComponent<TMP_Dropdown>();
            go.GetComponent<Image>().color = Paleta.Boton;
            dd.captionText.color = Paleta.Texto;
            dd.captionText.fontSize = 15;
            dd.itemText.color = Paleta.Texto;
            dd.itemText.fontSize = 14;
            foreach (Image i in dd.template.GetComponentsInChildren<Image>(true))
            {
                if (i.gameObject.name == "Template") i.color = Paleta.Tarjeta;
                else if (i.gameObject.name == "Item Background") i.color = Paleta.Boton;
                else if (i.gameObject.name == "Item Checkmark") i.color = Paleta.Acento;
            }
            Transform flecha = go.transform.Find("Arrow");
            if (flecha != null) flecha.GetComponent<Image>().color = Paleta.TextoSuave;
            ColorBlock cb = dd.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.selectedColor = Color.white;
            dd.colors = cb;
            RectTransform tpl = dd.template;
            tpl.sizeDelta = new Vector2(tpl.sizeDelta.x, 190f);
            return dd;
        }

        private static Button Boton(string nombre, RectTransform padre, string texto, float x, float y, float ancho, float alto,
                                    Color fondo, Color colorTexto)
        {
            GameObject go = TMP_DefaultControls.CreateButton(recursos);
            go.name = nombre;
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            if (ancho < 0) UIFabrica.FilaArriba(rt, y, alto, x, x);
            else UIFabrica.ArribaIzq(rt, x, y, ancho, alto);
            go.GetComponent<Image>().color = fondo;
            TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
            t.text = texto;
            t.color = colorTexto;
            t.fontSize = 15;
            t.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static Button BotonFijo(string nombre, RectTransform padre, string texto, float x, float ancho)
        {
            Button b = Boton(nombre, padre, texto, x, 0, ancho, 28, Paleta.Boton, Paleta.Texto);
            b.GetComponentInChildren<TMP_Text>(true).fontSize = 13;
            return b;
        }

        private static void ItemLeyenda(RectTransform padre, float x, Color color, string texto, float anchoMuestra)
        {
            Image m = UIFabrica.Imagen("Muestra " + texto, padre, color);
            RectTransform mr = m.rectTransform;
            mr.anchorMin = mr.anchorMax = new Vector2(0, 0.5f);
            mr.pivot = new Vector2(0, 0.5f);
            mr.anchoredPosition = new Vector2(x, 0);
            mr.sizeDelta = new Vector2(anchoMuestra, 14f);
            TextMeshProUGUI t = UIFabrica.Texto(texto, padre, texto, 13, Paleta.Texto, TextAlignmentOptions.MidlineLeft);
            RectTransform tr = t.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0, 0.5f);
            tr.pivot = new Vector2(0, 0.5f);
            tr.anchoredPosition = new Vector2(x + 20f, 0);
            tr.sizeDelta = new Vector2(100f, 24f);
        }
    }
}
