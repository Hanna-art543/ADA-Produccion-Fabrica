using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Fabrica.Core;
using UnityEngine;

// PERSONA 3 - Escena y animación.
//
// Dibuja un PlanResult como una fábrica en 3D y lo reproduce en el tiempo:
//   - 3 zonas (Corte -> Ensamblaje -> Empaquetado) con las máquinas como cubos de color.
//   - Una esfera por pieza que viaja Entrada -> zona 1 -> zona 2 -> zona 3 -> Salida
//     según el inicio/fin de cada Assignment.
//   - Barras de energía por máquina y un contador global (se pone rojo cerca de E_max).
//   - Play / Pausa / Reiniciar / velocidad (métodos públicos + HudFabrica opcional).
//
// Todo se construye por código: basta con agregar este componente a un GameObject vacío.
// La Persona 4 solo necesita llamar a  CargarPlan(PlanResult)  desde su UI.
public class VisualizadorFabrica : MonoBehaviour
{
    // ------------------------------------------------------------------ parámetros
    [Header("Carga inicial (la Persona 4 puede desactivarla y llamar a CargarPlan)")]
    public bool cargarAlIniciar = true;
    public int escenarioInicial = 1;
    public string metodoInicial = "greedy";

    [Header("Reproducción")]
    public bool reproducirAlCargar = true;
    [Tooltip("Segundos reales que dura la animación completa con velocidad = 1")]
    public float duracionBaseSegundos = 40f;
    [Range(0.1f, 16f)] public float velocidad = 1f;

    [Header("Aspecto")]
    public float separacionZonas = 8f;
    public float separacionMaquinas = 2.4f;
    public float tamanoCubo = 1.6f;
    public float radioEsfera = 0.32f;
    [Tooltip("Duración máxima del viaje entre estaciones, como fracción del makespan")]
    [Range(0.01f, 0.3f)] public float fraccionViaje = 0.06f;
    [Tooltip("Duración mínima del viaje (para que nunca sea un salto instantáneo)")]
    [Range(0.002f, 0.1f)] public float fraccionViajeMinimo = 0.015f;
    public float alturaArco = 0.9f;

    [Header("Energía")]
    [Tooltip("Fracción de E_max a partir de la cual el contador queda completamente rojo")]
    [Range(0.5f, 1f)] public float umbralAlerta = 0.9f;
    public float alturaMaxBarras = 2.4f;

    [Header("Cámara (desactivar si la Persona 4 maneja su propia cámara)")]
    public bool controlarCamara = true;
    [Range(20f, 80f)] public float inclinacionCamara = 52f;
    public float margenCamara = 1.08f;

    // ------------------------------------------------------------------ eventos
    public event Action<PlanResult> PlanCargado;
    public event Action Terminado;

    // ------------------------------------------------------------------ estado público (solo lectura)
    public PlanResult Plan { get { return plan; } }
    public bool HayPlan { get { return piezas != null && piezas.Count > 0; } }
    public bool Reproduciendo { get { return reproduciendo; } }
    public float Velocidad { get { return velocidad; } }
    public float Tiempo { get { return tiempo; } }
    public float TiempoInicio { get { return tInicio; } }
    public float TiempoFin { get { return tFin; } }
    public float Makespan { get { return makespan; } }
    public float EnergiaAcumulada { get { return energiaAcumulada; } }
    public float EnergiaMax { get { return energiaMaxPlan; } }
    public float FraccionEnergia { get { return energiaMaxPlan > 0f ? energiaAcumulada / energiaMaxPlan : 0f; } }
    public int PiezasTerminadas { get { return piezasTerminadas; } }
    public int TotalPiezas { get { return piezas == null ? 0 : piezas.Count; } }
    public int EscenarioActual { get { return escenarioActual; } }
    public string MetodoActual { get { return metodoActual; } }

    // ------------------------------------------------------------------ clases internas
    private class Maquina
    {
        public int id;
        public Color color;
        public float total;           // energía total de la máquina en este plan
        public float acumulada;       // energía consumida hasta el tiempo actual
        public Transform barra;
        public TextMesh valor;
        public Vector3 baseBarra;
        public Material matBarra;
    }

    private class Estacion
    {
        public Maquina maq;
        public int zona;
        public Vector3 pos;
        public Material mat;
        public bool ocupada;
        public List<Assignment> tareas = new List<Assignment>();
    }

    private class Pieza
    {
        public int id;
        public Assignment[] et = new Assignment[Etapas.Cantidad];
        public Estacion[] est = new Estacion[Etapas.Cantidad];
        public float[] viajeIni = new float[Etapas.Cantidad]; // instante en que sale hacia la etapa k
        public float salidaIni, salidaFin;
        public Vector3 posEntrada, posSalida;
        public Transform esfera;
    }

    // ------------------------------------------------------------------ campos privados
    private PlanResult plan;
    private int escenarioActual;
    private string metodoActual;

    private Transform raiz;
    private Camera cam;
    private float ultimoAspecto;
    private Material matBase;
    private static Font fuente;

    private readonly List<Material> materiales = new List<Material>();
    private readonly List<Transform> etiquetas = new List<Transform>();
    private List<Pieza> piezas;
    private List<Estacion> estaciones;
    private SortedDictionary<int, Maquina> maquinas;

    private Transform medidorRelleno;
    private Material matMedidor;
    private TextMesh textoEnergia, textoTiempo;

    private bool reproduciendo;
    private bool sucio;
    private float tiempo, tInicio, tFin, makespan;
    private float tViaje, tMin, tAsentamiento;
    private float energiaAcumulada, energiaMaxPlan, maxEnergiaMaquina;
    private int piezasTerminadas;
    private float anchoMedidor, xMedidor;
    private float extX = 22f, zMin = -12f, zMax = 12f;
    private Coroutine cargaEnCurso;

    // ================================================================== ciclo de vida
    private void Start()
    {
        if (cargarAlIniciar) CargarEscenario(escenarioInicial, metodoInicial);
    }

    private void OnDestroy()
    {
        LimpiarEscena();
    }

    private void Update()
    {
        if (HayPlan)
        {
            if (reproduciendo)
            {
                float unidadesPorSegundo = (tFin - tInicio) / Mathf.Max(1f, duracionBaseSegundos);
                tiempo += Time.deltaTime * unidadesPorSegundo * velocidad;
                if (tiempo >= tFin)
                {
                    tiempo = tFin;
                    reproduciendo = false;
                    sucio = true;
                    if (Terminado != null) Terminado();
                }
                sucio = true;
            }
            if (sucio)
            {
                ActualizarVista();
                sucio = false;
            }
        }

        if (cam != null && Mathf.Abs(cam.aspect - ultimoAspecto) > 0.01f) EncuadrarCamara();

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space)) AlternarPausa();
        if (Input.GetKeyDown(KeyCode.R)) Reiniciar();
        if (Input.GetKeyDown(KeyCode.UpArrow)) FijarVelocidad(velocidad * 2f);
        if (Input.GetKeyDown(KeyCode.DownArrow)) FijarVelocidad(velocidad * 0.5f);
#endif
    }

    private void LateUpdate()
    {
        // Los textos miran siempre hacia la cámara
        if (cam == null) return;
        Quaternion rot = cam.transform.rotation;
        for (int i = 0; i < etiquetas.Count; i++)
            if (etiquetas[i] != null) etiquetas[i].rotation = rot;
    }

    // ================================================================== API pública
    // Punto de entrada para la Persona 4: dibuja y reproduce el plan recibido.
    public void CargarPlan(PlanResult nuevo)
    {
        CargarPlan(nuevo, reproducirAlCargar);
    }

    public void CargarPlan(PlanResult nuevo, bool reproducir)
    {
        LimpiarEscena();
        plan = nuevo;
        ObtenerCamara();
        AsegurarLuz();

        if (nuevo == null)
        {
            MostrarMensaje("No hay plan para mostrar");
            return;
        }
        if (nuevo.asignaciones == null || nuevo.asignaciones.Count == 0)
        {
            string motivo = string.IsNullOrEmpty(nuevo.notas) ? "El plan no tiene asignaciones" : nuevo.notas;
            MostrarMensaje(nuevo.algoritmo + " - " + nuevo.escenario + "\n" + motivo);
            plan = nuevo;
            if (PlanCargado != null) PlanCargado(nuevo);
            return;
        }

        Construir(nuevo);
        tiempo = tInicio;
        reproduciendo = reproducir;
        sucio = true;
        ActualizarVista();
        sucio = false;
        if (PlanCargado != null) PlanCargado(nuevo);
    }

    // Lee escenarioK_<clave>.json de StreamingAssets/Resultados y lo carga.
    public void CargarEscenario(int escenario, string clave)
    {
        escenarioActual = escenario;
        metodoActual = clave;
        if (cargaEnCurso != null) StopCoroutine(cargaEnCurso);
        cargaEnCurso = StartCoroutine(CargadorResultados.CargarPlan(
            escenario, clave,
            p => CargarPlan(p),
            err =>
            {
                Debug.LogWarning(err);
                MostrarMensaje("No se pudo cargar el plan\n" + CargadorResultados.NombreArchivo(escenario, clave));
            }));
    }

    public void Play()
    {
        if (!HayPlan) return;
        if (tiempo >= tFin - 1e-4f) tiempo = tInicio;
        reproduciendo = true;
        sucio = true;
    }

    public void Pausa()
    {
        reproduciendo = false;
        sucio = true;
    }

    public void AlternarPausa()
    {
        if (reproduciendo) Pausa(); else Play();
    }

    // Vuelve al inicio y reproduce.
    public void Reiniciar()
    {
        if (!HayPlan) return;
        tiempo = tInicio;
        reproduciendo = true;
        sucio = true;
    }

    // Vuelve al inicio y se queda en pausa.
    public void ReiniciarEnPausa()
    {
        if (!HayPlan) return;
        tiempo = tInicio;
        reproduciendo = false;
        sucio = true;
    }

    public void FijarVelocidad(float v)
    {
        velocidad = Mathf.Clamp(v, 0.1f, 16f);
    }

    // Salta a un instante del plan (para el slider del HUD).
    public void FijarTiempo(float t)
    {
        if (!HayPlan) return;
        tiempo = Mathf.Clamp(t, tInicio, tFin);
        sucio = true;
    }

    // ================================================================== construcción de la escena
    private void Construir(PlanResult p)
    {
        raiz = new GameObject("FabricaRuntime").transform;

        maquinas = new SortedDictionary<int, Maquina>();
        estaciones = new List<Estacion>();
        piezas = new List<Pieza>();
        var zonas = new List<Estacion>[Etapas.Cantidad];
        for (int z = 0; z < Etapas.Cantidad; z++) zonas[z] = new List<Estacion>();
        var porClave = new Dictionary<long, Estacion>();
        var porPieza = new Dictionary<int, Pieza>();

        // 1) Máquinas, estaciones (máquina x zona) y piezas a partir de las asignaciones
        makespan = 0f;
        float energiaSuma = 0f;
        foreach (Assignment a in p.asignaciones)
        {
            int z = (int)a.etapa;
            if (z < 0 || z >= Etapas.Cantidad) continue;

            Maquina maq;
            if (!maquinas.TryGetValue(a.maquinaId, out maq))
            {
                maq = new Maquina { id = a.maquinaId, color = ColorMaquina(a.maquinaId) };
                maquinas.Add(a.maquinaId, maq);
            }

            long clave = z * 100000L + a.maquinaId;
            Estacion est;
            if (!porClave.TryGetValue(clave, out est))
            {
                est = new Estacion { maq = maq, zona = z };
                porClave.Add(clave, est);
                zonas[z].Add(est);
                estaciones.Add(est);
            }
            est.tareas.Add(a);
            maq.total += (float)a.energia;
            energiaSuma += (float)a.energia;
            makespan = Mathf.Max(makespan, (float)a.fin);

            Pieza pz;
            if (!porPieza.TryGetValue(a.piezaId, out pz))
            {
                pz = new Pieza { id = a.piezaId };
                porPieza.Add(a.piezaId, pz);
            }
            pz.et[z] = a;
            pz.est[z] = est;
        }

        foreach (Pieza pz in porPieza.Values)
        {
            bool completa = true;
            for (int k = 0; k < Etapas.Cantidad; k++) if (pz.et[k] == null) completa = false;
            if (completa) piezas.Add(pz);
            else Debug.LogWarning("La pieza " + pz.id + " no tiene las 3 etapas; no se dibuja.");
        }
        piezas.Sort((x, y) => x.id.CompareTo(y.id));

        maxEnergiaMaquina = 0f;
        foreach (Maquina m in maquinas.Values) maxEnergiaMaquina = Mathf.Max(maxEnergiaMaquina, m.total);

        energiaMaxPlan = p.energiaMax > 0 ? (float)p.energiaMax : (p.energiaTotal > 0 ? (float)p.energiaTotal : energiaSuma);
        if (energiaMaxPlan <= 0f) energiaMaxPlan = 1f;

        // 2) Escala de tiempos de la animación
        makespan = Mathf.Max(makespan, 1f);
        tViaje = Mathf.Max(0.5f, fraccionViaje * makespan);
        tMin = Mathf.Max(0.1f, fraccionViajeMinimo * makespan);
        tAsentamiento = Mathf.Max(0.05f, tMin * 0.8f);
        tInicio = -tViaje;           // pre-roll: las piezas que empiezan en t = 0 salen de la cola
        tFin = makespan + tViaje;    // post-roll: la última pieza llega a la salida

        // 3) Geometría: suelos, zonas, estaciones
        int maxPorZona = 1;
        for (int z = 0; z < Etapas.Cantidad; z++) maxPorZona = Mathf.Max(maxPorZona, zonas[z].Count);
        float profundidad = Mathf.Max(8f, maxPorZona * separacionMaquinas + 2.5f);
        float xEntrada = -2.15f * separacionZonas;
        float xSalida = 2.15f * separacionZonas;

        string[] nombresZona = { "1. CORTE", "2. ENSAMBLAJE", "3. EMPAQUETADO" };
        Color[] coloresZona =
        {
            new Color(0.17f, 0.26f, 0.36f), new Color(0.20f, 0.33f, 0.24f), new Color(0.37f, 0.28f, 0.20f)
        };
        Color gris = new Color(0.22f, 0.23f, 0.26f);
        Material tmp;

        for (int z = 0; z < Etapas.Cantidad; z++)
        {
            float x = (z - 1) * separacionZonas;
            Primitiva(PrimitiveType.Cube, "Zona_" + z, new Vector3(x, -0.1f, 0f),
                      new Vector3(separacionZonas * 0.9f, 0.2f, profundidad), coloresZona[z], out tmp);
            CrearTexto(nombresZona[z], new Vector3(x, 0.9f, profundidad / 2f + 0.9f), 0.9f, Color.white, TextAnchor.MiddleCenter);

            zonas[z].Sort((a, b) => a.maq.id.CompareTo(b.maq.id));
            int n = zonas[z].Count;
            for (int i = 0; i < n; i++)
            {
                Estacion e = zonas[z][i];
                float zPos = (i - (n - 1) / 2f) * separacionMaquinas;
                e.pos = new Vector3(x, tamanoCubo / 2f, zPos);
                Primitiva(PrimitiveType.Cube, "Maquina_M" + e.maq.id + "_zona" + z, e.pos,
                          Vector3.one * tamanoCubo, Atenuar(e.maq.color), out e.mat);
                CrearTexto("M" + e.maq.id, new Vector3(x, tamanoCubo * 0.45f, zPos - tamanoCubo / 2f - 0.45f),
                           0.55f, Color.white, TextAnchor.MiddleCenter);
            }
        }

        Primitiva(PrimitiveType.Cube, "Entrada", new Vector3(xEntrada, -0.1f, 0f),
                  new Vector3(4.2f, 0.2f, profundidad), gris, out tmp);
        Primitiva(PrimitiveType.Cube, "Salida", new Vector3(xSalida, -0.1f, 0f),
                  new Vector3(4.2f, 0.2f, profundidad), gris, out tmp);
        CrearTexto("ENTRADA", new Vector3(xEntrada, 0.9f, profundidad / 2f + 0.9f), 0.9f, Color.white, TextAnchor.MiddleCenter);
        CrearTexto("SALIDA", new Vector3(xSalida, 0.9f, profundidad / 2f + 0.9f), 0.9f, Color.white, TextAnchor.MiddleCenter);

        // 4) Ventanas de viaje de cada pieza
        foreach (Pieza pz in piezas)
        {
            for (int k = 0; k < Etapas.Cantidad; k++)
            {
                float ini = (float)pz.et[k].inicio;
                float ws = ini - tViaje;
                if (k > 0) ws = Mathf.Max((float)pz.et[k - 1].fin, ws);
                if (ini - ws < tMin) ws = ini - tMin;                              // viaje nunca instantáneo
                if (k > 0) ws = Mathf.Max(ws, (float)pz.et[k - 1].inicio);
                if (ws > ini - 1e-4f) ws = ini - 1e-4f;
                pz.viajeIni[k] = ws;
            }
            pz.salidaIni = (float)pz.et[Etapas.Cantidad - 1].fin;
            pz.salidaFin = pz.salidaIni + tViaje;
        }

        // 5) Esferas: cola de entrada (orden de salida) y cola de salida (orden de llegada)
        int filas = Mathf.Max(1, Mathf.FloorToInt((profundidad - 1.5f) / 0.8f));
        var ordenEntrada = new List<Pieza>(piezas);
        ordenEntrada.Sort((a, b) =>
        {
            int c = a.viajeIni[0].CompareTo(b.viajeIni[0]);
            return c != 0 ? c : a.id.CompareTo(b.id);
        });
        for (int i = 0; i < ordenEntrada.Count; i++)
            ordenEntrada[i].posEntrada = new Vector3(xEntrada - 1.4f + (i / filas) * 0.8f, radioEsfera,
                                                     -profundidad / 2f + 1f + (i % filas) * 0.8f);

        var ordenSalida = new List<Pieza>(piezas);
        ordenSalida.Sort((a, b) =>
        {
            int c = a.salidaIni.CompareTo(b.salidaIni);
            return c != 0 ? c : a.id.CompareTo(b.id);
        });
        for (int i = 0; i < ordenSalida.Count; i++)
            ordenSalida[i].posSalida = new Vector3(xSalida - 1.4f + (i / filas) * 0.8f, radioEsfera,
                                                   -profundidad / 2f + 1f + (i % filas) * 0.8f);

        foreach (Pieza pz in piezas)
        {
            float hue = Mathf.Repeat(pz.id * 0.61803398f, 1f);
            pz.esfera = Primitiva(PrimitiveType.Sphere, "Pieza_" + pz.id, pz.posEntrada,
                                  Vector3.one * radioEsfera * 2f, Color.HSVToRGB(hue, 0.65f, 1f), out tmp);
        }

        // 5b) Panel de energía por máquina (frente de la escena)
        int nM = maquinas.Count;
        float zPanel = -profundidad / 2f - 3.6f;
        int idx = 0;
        foreach (Maquina m in maquinas.Values)
        {
            float x = (idx - (nM - 1) / 2f) * 2.4f;
            m.baseBarra = new Vector3(x, 0f, zPanel);
            m.barra = Primitiva(PrimitiveType.Cube, "Barra_M" + m.id, m.baseBarra + Vector3.up * 0.02f,
                                new Vector3(0.9f, 0.04f, 0.9f), m.color, out m.matBarra);
            CrearTexto("M" + m.id, new Vector3(x, 0.35f, zPanel - 0.95f), 0.6f, Color.white, TextAnchor.MiddleCenter);
            m.valor = CrearTexto("0", new Vector3(x, 0.7f, zPanel), 0.5f, Color.white, TextAnchor.MiddleCenter);
            idx++;
        }
        CrearTexto("Energía por máquina", new Vector3(0f, 0.3f, zPanel - 1.9f), 0.6f,
                   new Color(0.8f, 0.8f, 0.85f), TextAnchor.MiddleCenter);

        // 5c) Medidor global de energía (fondo de la escena)
        float zG = profundidad / 2f + 3.4f;
        anchoMedidor = 2f * xSalida;
        xMedidor = -anchoMedidor / 2f;
        Primitiva(PrimitiveType.Cube, "Medidor_Fondo", new Vector3(0f, 0.15f, zG),
                  new Vector3(anchoMedidor, 0.3f, 0.9f), new Color(0.12f, 0.12f, 0.14f), out tmp);
        medidorRelleno = Primitiva(PrimitiveType.Cube, "Medidor_Relleno", new Vector3(xMedidor, 0.2f, zG - 0.05f),
                                   new Vector3(0.001f, 0.4f, 0.9f), Color.green, out matMedidor);
        Primitiva(PrimitiveType.Cube, "Medidor_Umbral", new Vector3(xMedidor + anchoMedidor * umbralAlerta, 0.3f, zG),
                  new Vector3(0.08f, 0.6f, 1.1f), new Color(1f, 1f, 1f), out tmp);
        textoEnergia = CrearTexto("", new Vector3(0f, 1.3f, zG + 0.2f), 0.8f, Color.white, TextAnchor.MiddleCenter);
        string estado = p.factible ? "" : "  |  NO FACTIBLE";
        CrearTexto(string.Format(CultureInfo.InvariantCulture, "{0} - {1}  |  Makespan {2:F1}  |  Costo {3:F1}{4}",
                                 p.algoritmo, p.escenario, p.makespan, p.costoTotal, estado),
                   new Vector3(xMedidor, 2.4f, zG + 0.2f), 0.6f, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleLeft);
        textoTiempo = CrearTexto("", new Vector3(xMedidor + anchoMedidor, 2.4f, zG + 0.2f), 0.6f,
                                 new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleRight);

        // 6) Límites para la cámara
        extX = xSalida + 3.5f;
        zMin = zPanel - 3f;
        zMax = zG + 3.5f;
        EncuadrarCamara();
    }

    // ================================================================== animación
    private void ActualizarVista()
    {
        if (!HayPlan) return;
        float t = tiempo;

        // Esferas
        piezasTerminadas = 0;
        float pulso = reproduciendo ? 1f + 0.12f * Mathf.Sin(Time.time * 8f) : 1f;
        foreach (Pieza pz in piezas)
        {
            bool procesando;
            pz.esfera.position = PosicionPieza(pz, t, out procesando);
            pz.esfera.localScale = Vector3.one * radioEsfera * 2f * (procesando ? pulso : 1f);
            if (t >= (float)pz.et[Etapas.Cantidad - 1].fin) piezasTerminadas++;
        }

        // Máquinas: brillantes si están trabajando, apagadas si están libres
        foreach (Estacion e in estaciones)
        {
            bool ocupada = false;
            for (int i = 0; i < e.tareas.Count; i++)
            {
                Assignment a = e.tareas[i];
                if (t >= a.inicio && t < a.fin) { ocupada = true; break; }
            }
            if (ocupada != e.ocupada)
            {
                e.ocupada = ocupada;
                e.mat.color = ocupada ? e.maq.color : Atenuar(e.maq.color);
            }
        }

        // Energía acumulada (cada tarea gasta su energía de forma lineal entre inicio y fin)
        foreach (Maquina m in maquinas.Values) m.acumulada = 0f;
        float total = 0f;
        foreach (Assignment a in plan.asignaciones)
        {
            Maquina m;
            if (!maquinas.TryGetValue(a.maquinaId, out m)) continue;
            float dur = (float)(a.fin - a.inicio);
            float frac = dur <= 0f ? (t >= a.fin ? 1f : 0f) : Mathf.Clamp01((t - (float)a.inicio) / dur);
            float e = (float)a.energia * frac;
            m.acumulada += e;
            total += e;
        }
        energiaAcumulada = total;

        foreach (Maquina m in maquinas.Values)
        {
            float rel = maxEnergiaMaquina > 0f ? m.acumulada / maxEnergiaMaquina : 0f;
            float h = Mathf.Max(0.04f, alturaMaxBarras * rel);
            m.barra.localScale = new Vector3(0.9f, h, 0.9f);
            m.barra.localPosition = m.baseBarra + Vector3.up * (h / 2f);
            m.valor.transform.localPosition = m.baseBarra + Vector3.up * (h + 0.5f);
            Texto(m.valor, m.acumulada.ToString("F0", CultureInfo.InvariantCulture));
        }

        // Contador global
        float fraccion = FraccionEnergia;
        float visual = Mathf.Clamp01(fraccion);
        float ancho = Mathf.Max(0.001f, anchoMedidor * visual);
        medidorRelleno.localScale = new Vector3(ancho, 0.4f, 0.9f);
        Vector3 pr = medidorRelleno.localPosition;
        medidorRelleno.localPosition = new Vector3(xMedidor + ancho / 2f, pr.y, pr.z);
        Color col = ColorEnergia(fraccion);
        matMedidor.color = col;
        textoEnergia.color = fraccion >= umbralAlerta ? new Color(1f, 0.3f, 0.25f) : Color.white;
        Texto(textoEnergia, string.Format(CultureInfo.InvariantCulture,
              "Energía total: {0:F1} / {1:F1}   ({2:F1} %)", energiaAcumulada, energiaMaxPlan, fraccion * 100f));
        Texto(textoTiempo, string.Format(CultureInfo.InvariantCulture,
              "t = {0:F1} / {1:F1}   |   Piezas terminadas: {2} / {3}",
              Mathf.Clamp(t, 0f, makespan), makespan, piezasTerminadas, piezas.Count));
    }

    // Posición de una pieza en el instante t (función pura del tiempo: sirve para pausar o saltar).
    private Vector3 PosicionPieza(Pieza p, float t, out bool procesando)
    {
        procesando = false;
        for (int k = 0; k < Etapas.Cantidad; k++)
        {
            Assignment a = p.et[k];
            float ini = p.viajeIni[k];       // sale hacia la etapa k
            float llega = (float)a.inicio;   // llega y empieza a procesar
            float fin = (float)a.fin;

            if (t < ini) return k == 0 ? p.posEntrada : TrasEtapa(p, k - 1, t);

            if (t < llega)
            {
                Vector3 origen = k == 0 ? p.posEntrada : TrasEtapa(p, k - 1, ini);
                return Viaje(origen, PosProceso(p.est[k]), (t - ini) / (llega - ini));
            }
            if (t < fin)
            {
                procesando = true;
                return PosProceso(p.est[k]);
            }
        }

        Vector3 desde = PosProceso(p.est[Etapas.Cantidad - 1]);
        if (t < p.salidaFin) return Viaje(desde, p.posSalida, (t - p.salidaIni) / (p.salidaFin - p.salidaIni));
        return p.posSalida;
    }

    // Después de terminar la etapa k la pieza baja de la máquina y espera a su lado.
    private Vector3 TrasEtapa(Pieza p, int k, float t)
    {
        float u = Mathf.Clamp01((t - (float)p.et[k].fin) / tAsentamiento);
        u = u * u * (3f - 2f * u);
        return Vector3.Lerp(PosProceso(p.est[k]), PosEspera(p.est[k], p.id), u);
    }

    private Vector3 PosProceso(Estacion e)
    {
        return e.pos + Vector3.up * (tamanoCubo / 2f + radioEsfera + 0.04f);
    }

    private Vector3 PosEspera(Estacion e, int piezaId)
    {
        float dz = ((piezaId % 3) - 1) * 0.45f;
        return new Vector3(e.pos.x + tamanoCubo / 2f + radioEsfera + 0.35f, radioEsfera, e.pos.z + dz);
    }

    private Vector3 Viaje(Vector3 a, Vector3 b, float u)
    {
        u = Mathf.Clamp01(u);
        float s = u * u * (3f - 2f * u);
        Vector3 pos = Vector3.Lerp(a, b, s);
        pos.y += Mathf.Sin(Mathf.PI * u) * alturaArco;
        return pos;
    }

    // ================================================================== utilidades de dibujo
    private void MostrarMensaje(string mensaje)
    {
        LimpiarEscena();
        ObtenerCamara();
        AsegurarLuz();
        raiz = new GameObject("FabricaRuntime").transform;
        CrearTexto(mensaje, new Vector3(0f, 2f, 0f), 1.1f, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
        extX = 14f; zMin = -6f; zMax = 6f;
        EncuadrarCamara();
    }

    private void LimpiarEscena()
    {
        reproduciendo = false;
        if (raiz != null) Destroy(raiz.gameObject);
        raiz = null;
        for (int i = 0; i < materiales.Count; i++) if (materiales[i] != null) Destroy(materiales[i]);
        materiales.Clear();
        etiquetas.Clear();
        piezas = null;
        estaciones = null;
        maquinas = null;
        medidorRelleno = null;
        textoEnergia = null;
        textoTiempo = null;
        energiaAcumulada = 0f;
        piezasTerminadas = 0;
        tiempo = tInicio = tFin = makespan = 0f;
    }

    private Transform Primitiva(PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Color color, out Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        mat = new Material(MaterialBase());
        mat.color = color;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        materiales.Add(mat);
        return go.transform;
    }

    // Se copia el material por defecto de una primitiva: funciona en Built-in, URP y en builds WebGL
    // sin tener que buscar shaders por nombre.
    private Material MaterialBase()
    {
        if (matBase != null) return matBase;
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        matBase = tmp.GetComponent<Renderer>().sharedMaterial;
        Destroy(tmp);
        return matBase;
    }

    private TextMesh CrearTexto(string texto, Vector3 pos, float altura, Color color, TextAnchor ancla)
    {
        GameObject go = new GameObject("Texto");
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = pos;
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = Fuente();
        tm.fontSize = 64;
        tm.characterSize = altura / 6.4f; // altura aproximada de una línea en unidades de mundo
        tm.anchor = ancla;
        tm.alignment = ancla == TextAnchor.MiddleLeft ? TextAlignment.Left
                     : ancla == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Center;
        tm.color = color;
        tm.text = texto;
        if (tm.font != null) go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        etiquetas.Add(go.transform);
        if (cam != null) go.transform.rotation = cam.transform.rotation;
        return tm;
    }

    private static Font Fuente()
    {
        if (fuente != null) return fuente;
#if UNITY_2022_2_OR_NEWER
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        fuente = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        return fuente;
    }

    private static void Texto(TextMesh tm, string s)
    {
        if (tm != null && tm.text != s) tm.text = s;
    }

    private static Color Atenuar(Color c)
    {
        return new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f);
    }

    private static Color ColorMaquina(int id)
    {
        Color[] paleta =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.25f, 0.60f, 0.95f), new Color(0.35f, 0.80f, 0.40f),
            new Color(0.95f, 0.75f, 0.20f), new Color(0.70f, 0.45f, 0.90f), new Color(0.95f, 0.55f, 0.20f),
            new Color(0.25f, 0.80f, 0.80f), new Color(0.90f, 0.45f, 0.70f)
        };
        if (id >= 0 && id < paleta.Length) return paleta[id];
        return Color.HSVToRGB(Mathf.Repeat(id * 0.61803398f, 1f), 0.6f, 0.95f);
    }

    // Verde -> amarillo -> rojo; rojo completo desde umbralAlerta (cerca de E_max).
    private Color ColorEnergia(float fraccion)
    {
        float f = Mathf.InverseLerp(0.5f, umbralAlerta, fraccion);
        Color verde = new Color(0.25f, 0.85f, 0.35f);
        Color amarillo = new Color(0.98f, 0.85f, 0.2f);
        Color rojo = new Color(0.95f, 0.2f, 0.2f);
        return f < 0.5f ? Color.Lerp(verde, amarillo, f * 2f) : Color.Lerp(amarillo, rojo, (f - 0.5f) * 2f);
    }

    // ================================================================== cámara y luz
    private Camera ObtenerCamara()
    {
        if (cam != null) return cam;
        cam = Camera.main;
        if (cam == null) cam = Buscar<Camera>();
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        return cam;
    }

    private void AsegurarLuz()
    {
        if (Buscar<Light>() != null) return;
        GameObject go = new GameObject("Luz direccional");
        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private void EncuadrarCamara()
    {
        if (!controlarCamara) return;
        Camera c = ObtenerCamara();
        if (c == null) return;

        c.orthographic = false;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
        c.transform.rotation = Quaternion.Euler(inclinacionCamara, 0f, 0f);
        ultimoAspecto = c.aspect;

        float vfov = c.fieldOfView * Mathf.Deg2Rad;
        float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov / 2f) * c.aspect);
        float dH = extX / Mathf.Tan(hfov / 2f);
        float altoVisible = (zMax - zMin) * Mathf.Sin(inclinacionCamara * Mathf.Deg2Rad) + 4f;
        float dV = (altoVisible / 2f) / Mathf.Tan(vfov / 2f);
        float d = Mathf.Max(dH, dV) * margenCamara;

        Vector3 centro = new Vector3(0f, 0.5f, (zMin + zMax) / 2f);
        c.transform.position = centro - c.transform.forward * d;
    }

    private static T Buscar<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }
}