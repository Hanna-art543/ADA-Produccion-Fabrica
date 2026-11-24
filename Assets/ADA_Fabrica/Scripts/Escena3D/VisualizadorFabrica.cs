using System;
using System.Collections.Generic;
using Fabrica.Core;
using Fabrica.UI;
using TMPro;
using UnityEngine;

namespace Fabrica.Escena
{
    /// <summary>
    /// Persona 3: construye la fábrica por código y anima el plan.
    ///  · Una LÍNEA de N máquinas (N = ResumenEscenario.maquinas, aunque el algoritmo no use alguna).
    ///    Cada máquina se pinta con el color de la etapa que procesa en ese instante (gris = ociosa),
    ///    con los mismos colores que el Gantt.
    ///  · Una esfera por pieza. Su posición es una función pura del tiempo (TiempoPiezas).
    ///  · Barra de energía por máquina y medidor global Σ energia·clamp01((t−inicio)/(fin−inicio)) contra E_max
    ///    (rojo desde el 90 %).
    ///  · ES EL RELOJ ÚNICO de la escena: implementa IRelojControlable, así que el cursor del Gantt
    ///    y los botones de la UI lo usan directamente (animación y Gantt van sincronizados).
    /// Datos: se suscribe a ControladorPlan (único lector: RepositorioPlanes). Sin ControladorPlan en la
    /// escena (escena de prueba de la Persona 3) carga el plan por su cuenta con CargadorResultados.
    /// </summary>
    public class VisualizadorFabrica : MonoBehaviour, IRelojControlable, IRelojBuscable
    {
        [Header("Datos")]
        [Tooltip("Solo si NO hay ControladorPlan en la escena (escena de prueba de la Persona 3)")]
        public bool cargarAlIniciar = true;
        public int escenarioInicial = 1;
        public string claveInicial = "greedy";

        [Header("Tiempo")]
        [Tooltip("Segundos que dura todo el plan a velocidad x1")]
        public float duracionBaseSegundos = 40f;
        [Range(0f, 0.2f)] public float fraccionViaje = 0.06f;
        [Range(0f, 0.1f)] public float fraccionViajeMinimo = 0.015f;
        public bool reproducirAlCargar = true;
        [Range(0f, 1f)] public float umbralAlerta = 0.9f;

        [Header("Cámara")]
        public bool controlarCamara = true;
        public Camera camara;
        [Range(15f, 80f)] public float inclinacionCamara = 46f;
        [Range(0f, 0.3f)] public float margenCamara = 0.06f;

        [Header("Aspecto")]
        public float pasoMaquinas = 3.2f;
        public Color colorOciosa = new Color(0.42f, 0.45f, 0.5f);
        public Color colorSinUso = new Color(0.22f, 0.24f, 0.27f);
        public Color colorPiso = new Color(0.16f, 0.18f, 0.21f);
        [Tooltip("Pone un gatito operador junto a cada máquina")]
        public bool mostrarGatitos = false;
        [Tooltip("Modelo del gatito (opcional). Si está vacío se arma uno con primitivas.")]
        public GameObject prefabGato;
        public float alturaGato = 1.0f;
        [Tooltip("Giro (Y) del gatito: 180 = mira a la cámara. Ajustar si el modelo importado mira para otro lado.")]
        public float giroGato = 145f;

        [Header("Modelos 3D (opcionales: sin ellos se usan primitivas)")]
        public GameObject modeloMaquina;
        public GameObject modeloCinta;
        [Tooltip("Forma de la pieza según etapas terminadas: 0 bruta, 1 cortada, 2 ensamblada, 3 empaquetada")]
        public GameObject[] modelosPieza = new GameObject[4];

        // ---- eventos (API de la Persona 3)
        public event Action<PlanResult> PlanCargado;
        public event Action Terminado;
        /// <summary>(media longitud de la línea, borde delantero, z del medidor): para que AmbienteFabrica arme la nave.</summary>
        public event Action<float, float, float> FabricaConstruida;

        // ---- estado del reloj
        private double tiempo;
        private bool reproduciendo;
        private float velocidad = 1f;

        // ---- plan actual
        private PlanResult plan;
        private ResumenEscenario resumen;
        private TiempoPiezas modelo;
        private ControladorPlan controlador;

        // ---- objetos generados
        private Transform raiz;
        private Material materialBase;
        private readonly List<Maquina> maquinas = new List<Maquina>();
        private readonly Dictionary<int, Maquina> maquinaPorId = new Dictionary<int, Maquina>();
        private readonly List<Transform> esferas = new List<Transform>();
        private readonly List<GameObject[]> formas = new List<GameObject[]>();
        private int[] formaMostrada = new int[0];
        private bool conModeloMaquina, conModelosPieza;
        private float alturaArriba = 1.45f, alturaPieza = 0.21f;
        private float anguloEngranaje;
        private readonly List<TMP_Text> etiquetas = new List<TMP_Text>();
        private Transform rellenoGlobal;
        private Material matRellenoGlobal;
        private float anchoMedidor;
        private TextMeshPro textoEnergia, textoInfo, textoMensaje;
        private Vector3 centroCola, centroSalida;
        private int columnasCola = 5;
        private float energiaMaqMax = 1f;
        private Bounds limites;
        private float aspectoPrevio = -1f;
        private Rect rectPrevio;

        private class Maquina
        {
            public int id;
            public Transform cuerpo;
            public Material material;
            public int etapaMostrada = -2;
            public Transform barra;
            public Material matBarra;
            public Material matPantalla, matBaliza;
            public Transform brazo, engranaje;
            public Quaternion brazoBase = Quaternion.identity, engranajeBase = Quaternion.identity;
            public TextMeshPro valor;
            public Transform gato;
            public Vector3 gatoBase;
            public Quaternion gatoRot = Quaternion.identity;   // rotación propia del modelo importado (p. ej. FBX de Blender)
            public bool usada;
        }

        // ================================================================== API (reloj)

        public double Tiempo { get { return tiempo; } }
        public double Makespan { get { return modelo != null ? modelo.Makespan : 0; } }
        public bool Reproduciendo { get { return reproduciendo; } }
        public float Velocidad { get { return velocidad; } set { FijarVelocidad(value); } }
        public double TiempoInicio { get { return 0; } }
        public double TiempoFin { get { return Makespan; } }

        public double EnergiaAcumulada { get { return modelo != null ? modelo.Energia(tiempo) : 0; } }
        public double EnergiaMax
        {
            get
            {
                if (plan != null && plan.energiaMax > 0) return plan.energiaMax;
                return resumen != null ? resumen.energiaMax : 0;
            }
        }
        public float FraccionEnergia { get { return EnergiaMax > 0 ? (float)(EnergiaAcumulada / EnergiaMax) : 0f; } }
        public int PiezasTerminadas { get { return modelo != null ? modelo.PiezasTerminadas(tiempo) : 0; } }
        public int TotalPiezas { get { return modelo != null ? modelo.CantidadPiezas : 0; } }
        public PlanResult PlanActual { get { return plan; } }

        public void Play()
        {
            if (Makespan <= 0) return;
            if (tiempo >= Makespan) tiempo = 0;
            reproduciendo = true;
        }

        public void Pausa() { reproduciendo = false; }
        public void AlternarPausa() { if (reproduciendo) Pausa(); else Play(); }

        /// <summary>Vuelve a 0 y sigue reproduciendo.</summary>
        public void Reiniciar() { tiempo = 0; reproduciendo = Makespan > 0; ActualizarVista(); }

        public void ReiniciarEnPausa() { tiempo = 0; reproduciendo = false; ActualizarVista(); }
        public void FijarVelocidad(float v) { velocidad = Mathf.Clamp(v, 0.1f, 16f); }
        public void FijarTiempo(float t) { IrA(t); }

        public void IrA(double t)
        {
            tiempo = t < 0 ? 0 : (t > Makespan ? Makespan : t);
            ActualizarVista();
        }

        // IRelojControlable
        public void Reproducir() { Play(); }
        public void Pausar() { Pausa(); }

        // ================================================================== carga

        private void OnEnable()
        {
            controlador = ControladorPlan.Buscar();
            if (controlador != null) controlador.Suscribir(AlPlanDelControlador);
        }

        private void OnDisable()
        {
            if (controlador != null) controlador.Desuscribir(AlPlanDelControlador);
        }

        private void Start()
        {
            if (controlador == null && cargarAlIniciar && plan == null) CargarEscenario(escenarioInicial, claveInicial);
        }

        private void AlPlanDelControlador(PlanResult p, ResumenEscenario r)
        {
            Construir(p, r, reproducirAlCargar);
        }

        /// <summary>Lee el JSON (claves: greedy, dc, pd, int, random, bb). Con ControladorPlan, pasa por él para que la UI se entere.</summary>
        public void CargarEscenario(int escenario, string clave)
        {
            if (controlador != null) { controlador.CargarPlan(escenario, clave); return; }
            PlanResult p = CargadorResultados.CargarPlan(escenario, clave);
            if (p == null) { Debug.LogWarning("[Fabrica] No existe el plan escenario" + escenario + "_" + clave); return; }
            Construir(p, CargadorResultados.CargarResumenEscenario(escenario), reproducirAlCargar);
        }

        public void CargarPlan(PlanResult p) { CargarPlan(p, reproducirAlCargar); }

        /// <summary>Dibuja un plan suelto (busca su escenario por nombre para saber cuántas máquinas hay).</summary>
        public void CargarPlan(PlanResult p, bool reproducir)
        {
            Construir(p, CargadorResultados.ResumenDePlan(p), reproducir);
        }

        // ================================================================== construcción

        private void Construir(PlanResult p, ResumenEscenario r, bool reproducir)
        {
            plan = p;
            resumen = r;
            modelo = new TiempoPiezas(p, fraccionViaje, fraccionViajeMinimo);
            tiempo = 0;
            reproduciendo = reproducir && modelo.Makespan > 0;

            if (raiz != null) Destroy(raiz.gameObject);
            raiz = new GameObject("FabricaGenerada").transform;
            raiz.SetParent(transform, false);
            maquinas.Clear(); maquinaPorId.Clear(); esferas.Clear(); etiquetas.Clear(); formas.Clear();
            if (materialBase == null) materialBase = MaterialPorDefecto();
            conModeloMaquina = modeloMaquina != null;
            conModelosPieza = modelosPieza != null && modelosPieza.Length >= 4 && modelosPieza[0] != null && modelosPieza[1] != null && modelosPieza[2] != null && modelosPieza[3] != null;
            alturaArriba = conModeloMaquina ? 1.29f : 1.2f;
            alturaPieza = conModelosPieza ? 0.18f : 0.21f;

            // ---- máquinas: la cantidad SIEMPRE del resumen
            int n = r != null && r.maquinas > 0 ? r.maquinas : MaquinasDelPlan(p);
            int baseIds = r != null ? RepositorioPlanes.BaseIdsMaquina(r.numero) : 0;
            float x0 = -(n - 1) * 0.5f * pasoMaquinas;
            energiaMaqMax = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                int id = baseIds + i;
                var m = new Maquina { id = id, usada = modelo.TareasDeMaquina(id) > 0 };
                Vector3 pos = new Vector3(x0 + i * pasoMaquinas, 0f, 0f);
                if (conModeloMaquina) MaquinaConModelo(m, pos);
                else
                {
                    m.cuerpo = Cubo("Maquina M" + id, pos + Vector3.up * 0.6f, new Vector3(1.8f, 1.2f, 1.5f), m.usada ? colorOciosa : colorSinUso, out m.material);
                    Cubo("Base", pos + Vector3.up * 0.04f, new Vector3(2.3f, 0.08f, 2.0f), new Color(0.11f, 0.12f, 0.14f));
                }
                Etiqueta("M" + id + (m.usada ? "" : "\n<size=60%>sin uso</size>"), pos + new Vector3(0f, conModeloMaquina ? 1.15f : 1.25f, -0.85f), 3.2f,
                         m.usada ? Color.white : new Color(0.75f, 0.78f, 0.82f), false);
                // cinta transportadora delante de la máquina (las piezas esperan sobre ella)
                Cinta(pos + new Vector3(0f, 0f, -1.65f));
                // barra de energía de la máquina (horizontal, encima)
                Cubo("Energia fondo M" + id, pos + new Vector3(0f, 2.3f, 0f), new Vector3(1.64f, 0.14f, 0.06f), new Color(0.08f, 0.09f, 0.1f));
                m.barra = Cubo("Energia M" + id, pos + new Vector3(-0.8f, 2.3f, -0.03f), new Vector3(0.001f, 0.1f, 0.06f), Paleta.Bien, out m.matBarra);
                m.valor = Etiqueta("", pos + new Vector3(0f, 2.55f, 0f), 2.2f, new Color(0.85f, 0.9f, 0.95f), true);
                energiaMaqMax = Mathf.Max(energiaMaqMax, (float)modelo.EnergiaMaquinaTotal(id));
                if (mostrarGatitos) CrearGato(m, pos + new Vector3(-pasoMaquinas * 0.5f, 0f, -0.2f), i);
                maquinas.Add(m);
                maquinaPorId[id] = m;
            }

            // ---- cola de ENTRADA (adelante a la izquierda) y SALIDA (adelante a la derecha), piso
            int piezas = modelo.CantidadPiezas;
            float mitadLinea = -x0 + 1.15f;                                   // media longitud de la línea de máquinas
            columnasCola = Mathf.Clamp(Mathf.FloorToInt((mitadLinea - 0.8f) / 0.55f), 3, 10);
            int filasCola = Mathf.Max(1, Mathf.CeilToInt(piezas / (float)columnasCola));
            float anchoCola = columnasCola * 0.55f;
            float profCola = Mathf.Max(1.4f, filasCola * 0.55f + 0.6f);
            float zColas = -2.75f - profCola * 0.5f;
            centroCola = new Vector3(-mitadLinea + anchoCola * 0.5f + 0.3f, 0.03f + alturaPieza, zColas);
            centroSalida = new Vector3(mitadLinea - anchoCola * 0.5f - 0.3f, 0.03f + alturaPieza, zColas);
            float xMin = x0 - (mostrarGatitos ? 2.0f : 1.4f), xMax = -x0 + 1.5f;
            float zFrente = zColas - profCola * 0.5f - 1.0f;
            float zFondo = 2.4f;
            Cubo("Piso", new Vector3((xMin + xMax) * 0.5f, -0.05f, (zFrente + zFondo) * 0.5f),
                 new Vector3(xMax - xMin + 1.2f, 0.1f, zFondo - zFrente + 0.8f), colorPiso);
            Cubo("Entrada", new Vector3(centroCola.x, 0.0f, centroCola.z), new Vector3(anchoCola + 0.6f, 0.06f, profCola), new Color(0.2f, 0.32f, 0.45f));
            Cubo("Salida", new Vector3(centroSalida.x, 0.0f, centroSalida.z), new Vector3(anchoCola + 0.6f, 0.06f, profCola), new Color(0.2f, 0.42f, 0.3f));
            Etiqueta("ENTRADA", new Vector3(centroCola.x, 0.1f, zColas - profCola * 0.5f - 0.45f), 3.6f, new Color(0.7f, 0.85f, 1f), false);
            Etiqueta("SALIDA", new Vector3(centroSalida.x, 0.1f, zColas - profCola * 0.5f - 0.45f), 3.6f, new Color(0.7f, 1f, 0.8f), false);

            // ---- medidor global de energía (al fondo)
            anchoMedidor = Mathf.Max(6f, mitadLinea * 1.6f);
            Vector3 cMed = new Vector3(0f, 3.5f, zFondo);
            Cubo("Medidor fondo", cMed, new Vector3(anchoMedidor + 0.12f, 0.42f, 0.1f), new Color(0.1f, 0.11f, 0.13f));
            rellenoGlobal = Cubo("Medidor relleno", cMed + new Vector3(-anchoMedidor * 0.5f, 0f, -0.04f), new Vector3(0.001f, 0.32f, 0.1f), Paleta.Bien, out matRellenoGlobal);
            Cubo("Marca 90%", cMed + new Vector3(-anchoMedidor * 0.5f + anchoMedidor * umbralAlerta, 0f, -0.09f), new Vector3(0.05f, 0.5f, 0.05f), Color.white);
            textoEnergia = Etiqueta("", cMed + new Vector3(0f, 0.6f, 0f), 5f, Color.white, true);
            textoInfo = Etiqueta("", cMed + new Vector3(0f, 1.25f, 0f), 4f, new Color(0.85f, 0.88f, 0.92f), true);
            textoEnergia.rectTransform.sizeDelta = textoInfo.rectTransform.sizeDelta = new Vector2(anchoMedidor + 10f, 2f);
            textoMensaje = Etiqueta("", new Vector3(0f, 2.0f, zColas), 5f, Paleta.Acento, true);
            textoMensaje.rectTransform.sizeDelta = new Vector2(Mathf.Max(10f, xMax - xMin), 3f);
            textoMensaje.enableWordWrapping = true;

            // ---- piezas (una esfera de color propio por pieza)
            formaMostrada = new int[piezas];
            for (int i = 0; i < piezas; i++)
            {
                Color c = Color.HSVToRGB((i * 0.618034f) % 1f, 0.55f, 0.95f);
                if (conModelosPieza)
                {
                    // una pieza = 4 formas (bruta, cortada, ensamblada, empaquetada); se ve la que corresponde a sus etapas terminadas
                    var e = new GameObject("Pieza " + modelo.IdPieza(i)).transform;
                    e.SetParent(raiz, false);
                    var f = new GameObject[4];
                    for (int k = 0; k < 4; k++)
                    {
                        Transform t = Instantiate(modelosPieza[k], e).transform;
                        Quaternion rm = t.localRotation;
                        t.localPosition = Vector3.zero;
                        t.localRotation = Quaternion.Euler(0f, 180f, 0f) * rm;
                        foreach (Collider col in t.GetComponentsInChildren<Collider>()) Destroy(col);
                        Teñir(t, "Color", c);
                        t.gameObject.SetActive(k == 0);
                        f[k] = t.gameObject;
                    }
                    formas.Add(f);
                    esferas.Add(e);
                }
                else
                {
                    Material mat;
                    esferas.Add(Primitiva(PrimitiveType.Sphere, "Pieza " + modelo.IdPieza(i), ColaPos(i), Vector3.one * 0.42f, c, out mat));
                }
            }

            // ---- mensajes
            bool vacio = p == null || p.asignaciones == null || p.asignaciones.Count == 0;
            if (vacio)
            {
                var pa = p as PlanResultAleatorio;
                bool aleatorio = pa != null || (p != null && CalculosFabrica.ClaveDeAlgoritmo(p.algoritmo) == "random");
                textoMensaje.text = aleatorio && (p == null || !p.factible) ? CalculosFabrica.MensajeSinFactibleAleatorio
                                  : (p != null && !string.IsNullOrEmpty(p.notas) ? p.notas : "Este plan no tiene asignaciones");
            }
            else textoMensaje.text = "";

            limites = new Bounds(new Vector3((xMin + xMax) * 0.5f, 2.3f, (zFrente + zFondo) * 0.5f),
                                 new Vector3(xMax - xMin + 0.4f, 4.6f, zFondo - zFrente + 0.4f));
            aspectoPrevio = -1f;
            ActualizarVista();
            if (FabricaConstruida != null) FabricaConstruida(mitadLinea, zFrente, zFondo);
            if (PlanCargado != null) PlanCargado(p);
        }

        private static int MaquinasDelPlan(PlanResult p)
        {
            int m = 0;
            if (p != null && p.asignaciones != null) foreach (Assignment a in p.asignaciones) m = Mathf.Max(m, a.maquinaId + 1);
            Debug.LogWarning("[Fabrica] Sin resumen del escenario: se usa el maquinaId máximo + 1 (" + m + "). Puede faltar alguna máquina sin uso.");
            return m;
        }

        // ================================================================== animación

        private void Update()
        {
            if (modelo == null) return;
            if (reproduciendo)
            {
                double ups = modelo.Makespan / Mathf.Max(0.01f, duracionBaseSegundos) * velocidad;
                tiempo += Time.deltaTime * ups;
                if (tiempo >= modelo.Makespan)
                {
                    tiempo = modelo.Makespan;
                    reproduciendo = false;
                    ActualizarVista();
                    if (Terminado != null) Terminado();
                    return;
                }
            }
            ActualizarVista();
        }

        private void LateUpdate()
        {
            Camera cam = Camara();
            if (cam == null) return;
            if (controlarCamara && raiz != null && (Mathf.Abs(cam.aspect - aspectoPrevio) > 0.001f || cam.rect != rectPrevio))
            {
                aspectoPrevio = cam.aspect;
                rectPrevio = cam.rect;
                Encuadrar(cam);
            }
            Quaternion rot = cam.transform.rotation;
            foreach (TMP_Text t in etiquetas) if (t != null) t.transform.rotation = rot;
        }

        private void ActualizarVista()
        {
            if (modelo == null || raiz == null) return;
            double t = tiempo;
            float pulso = reproduciendo ? 1f + 0.08f * Mathf.Sin(Time.time * 9f) : 1f;
            if (reproduciendo) anguloEngranaje += Time.deltaTime * 180f * Mathf.Max(0.5f, velocidad);

            foreach (Maquina m in maquinas)
            {
                int et = modelo.EtapaEnMaquina(m.id, t);
                if (et != m.etapaMostrada)
                {
                    m.etapaMostrada = et;
                    Color ce = et >= 0 ? Paleta.DeEtapa((Etapa)et) : (m.usada ? colorOciosa : colorSinUso);
                    if (m.material != null) m.material.color = ce;
                    if (m.matPantalla != null) m.matPantalla.color = et >= 0 ? Color.Lerp(ce, Color.white, 0.25f) : (m.usada ? new Color(0.12f, 0.16f, 0.22f) : new Color(0.04f, 0.04f, 0.05f));
                }
                if (m.matBaliza != null)
                    m.matBaliza.color = et >= 0 ? Color.Lerp(new Color(0.5f, 0.25f, 0.05f), new Color(1f, 0.65f, 0.1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f)) : new Color(0.25f, 0.2f, 0.15f);
                if (et >= 0 && reproduciendo)
                {
                    if (m.brazo != null) m.brazo.localRotation = m.brazoBase * Quaternion.Euler(0f, 40f * Mathf.Sin(Time.time * 2.5f + m.id), 0f);
                    if (m.engranaje != null) m.engranaje.localRotation = m.engranajeBase * Quaternion.Euler(anguloEngranaje + m.id * 20f, 0f, 0f);
                }
                float e = (float)modelo.EnergiaMaquina(m.id, t);
                float w = 1.6f * Mathf.Clamp01(e / energiaMaqMax);
                m.barra.localScale = new Vector3(Mathf.Max(0.001f, w), 0.1f, 0.06f);
                Vector3 bp = m.barra.localPosition; bp.x = MaqPos(m.id).x - 0.8f + w * 0.5f; m.barra.localPosition = bp;
                m.valor.text = e > 0.05f ? CalculosFabrica.N(e, 0) : "";
                if (m.gato != null)
                {
                    float a = et >= 0 ? 0.07f * Mathf.Abs(Mathf.Sin(Time.time * 7f + m.id)) : 0.015f * Mathf.Sin(Time.time * 2f + m.id);
                    m.gato.localPosition = m.gatoBase + Vector3.up * a;
                    m.gato.localRotation = Quaternion.Euler(0f, giroGato + (et >= 0 ? 8f * Mathf.Sin(Time.time * 5f + m.id) : 0f), 0f) * m.gatoRot;
                }
            }

            for (int i = 0; i < esferas.Count; i++)
            {
                TiempoPiezas.Estado s = modelo.EstadoPieza(i, t);
                Vector3 p;
                float esc = 0.42f;
                switch (s.tipo)
                {
                    case TiempoPiezas.Tipo.Cola: p = ColaPos(i); break;
                    case TiempoPiezas.Tipo.Viaje:
                        p = Arco(s.maquinaDesde < 0 ? ColaPos(i) : EsperaPos(s.maquinaDesde, i), ArribaPos(s.maquinaHasta), s.progreso);
                        break;
                    case TiempoPiezas.Tipo.Procesando: p = ArribaPos(s.maquinaHasta); esc *= pulso; break;
                    case TiempoPiezas.Tipo.Espera: p = EsperaPos(s.maquinaHasta, i); break;
                    case TiempoPiezas.Tipo.ViajeSalida: p = Arco(ArribaPos(s.maquinaDesde), SalidaPos(i), s.progreso); break;
                    default: p = SalidaPos(i); break;
                }
                esferas[i].localPosition = p;
                if (conModelosPieza)
                {
                    int forma = FormaPieza(s);
                    if (forma != formaMostrada[i])
                    {
                        for (int k = 0; k < 4; k++) formas[i][k].SetActive(k == forma);
                        formaMostrada[i] = forma;
                    }
                    esferas[i].localScale = Vector3.one * (esc / 0.42f);
                }
                else esferas[i].localScale = Vector3.one * esc;
            }

            // medidor global: Σ energia·clamp01(...) contra E_max, rojo desde el umbral
            double eTot = modelo.Energia(t);
            double emax = EnergiaMax;
            float f = emax > 0 ? (float)(eTot / emax) : 0f;
            float fv = Mathf.Clamp01(f);
            rellenoGlobal.localScale = new Vector3(Mathf.Max(0.001f, anchoMedidor * fv), 0.32f, 0.1f);
            Vector3 rp = rellenoGlobal.localPosition; rp.x = -anchoMedidor * 0.5f + anchoMedidor * fv * 0.5f; rellenoGlobal.localPosition = rp;
            Color cg = f >= umbralAlerta ? Paleta.Mal : (f < 0.6f ? Paleta.Bien : Color.Lerp(Paleta.Bien, Paleta.Acento, (f - 0.6f) / Mathf.Max(0.01f, umbralAlerta - 0.6f)));
            matRellenoGlobal.color = cg;
            string rojo = f >= umbralAlerta ? "<color=#E5606A>" : "";
            textoEnergia.text = rojo + "Energía total: " + CalculosFabrica.N(eTot, 1) + " / " + CalculosFabrica.N(emax, 1) +
                                "  (" + CalculosFabrica.N(f * 100f, 1) + " %)" + (rojo.Length > 0 ? "</color>" : "");
            string nombre = plan != null ? plan.algoritmo : "";
            textoInfo.text = nombre + "   ·   t = " + CalculosFabrica.N(t, 1) + " / " + CalculosFabrica.N(modelo.Makespan, 1) +
                             "   ·   piezas en salida " + modelo.PiezasTerminadas(t) + "/" + modelo.CantidadPiezas +
                             (plan != null && !plan.factible ? "   ·   <color=#E5606A>NO FACTIBLE</color>" : "");
        }

        // ---- posiciones (todas en coordenadas locales de la raíz)
        private Vector3 MaqPos(int id)
        {
            Maquina m;
            return maquinaPorId.TryGetValue(id, out m) ? new Vector3(m.cuerpo.localPosition.x, 0f, 0f) : Vector3.zero;
        }

        private Vector3 ArribaPos(int id) { return MaqPos(id) + new Vector3(0f, alturaArriba + alturaPieza, conModeloMaquina ? -0.05f : 0f); }

        /// <summary>Etapas terminadas (0..3) → forma de la pieza.</summary>
        private static int FormaPieza(TiempoPiezas.Estado s)
        {
            switch (s.tipo)
            {
                case TiempoPiezas.Tipo.Cola: return 0;
                case TiempoPiezas.Tipo.Viaje: return s.etapa;
                case TiempoPiezas.Tipo.Procesando: return s.etapa;
                case TiempoPiezas.Tipo.Espera: return Mathf.Min(3, s.etapa + 1);
                default: return 3;
            }
        }

        private Vector3 EsperaPos(int id, int i)
        {
            // esperando delante de su máquina (hasta 12 lugares distintos, para que no se encimen)
            return MaqPos(id) + new Vector3(((i % 4) - 1.5f) * 0.45f, 0.55f + alturaPieza, -1.42f - ((i / 4) % 2) * 0.46f);
        }

        private Vector3 ColaPos(int i)
        {
            int filas = Mathf.Max(1, Mathf.CeilToInt(TotalPiezas / (float)columnasCola));
            int c = i % columnasCola, f = i / columnasCola;
            return centroCola + new Vector3((c - (columnasCola - 1) * 0.5f) * 0.55f, 0f, (f - (filas - 1) * 0.5f) * 0.55f);
        }

        private Vector3 SalidaPos(int i)
        {
            return ColaPos(i) - centroCola + centroSalida;
        }

        private static Vector3 Arco(Vector3 a, Vector3 b, float u)
        {
            float s = u * u * (3f - 2f * u);
            Vector3 p = Vector3.Lerp(a, b, s);
            p.y += Mathf.Sin(s * Mathf.PI) * (0.8f + 0.08f * Vector3.Distance(a, b));
            return p;
        }

        // ================================================================== cámara

        private Camera Camara()
        {
            if (camara == null) camara = Camera.main;
            return camara;
        }

        /// <summary>Encaja toda la fábrica en el viewport de la cámara (que AreaLibreCamara deja en el hueco libre de la UI).</summary>
        private void Encuadrar(Camera cam)
        {
            Vector3 centro = transform.TransformPoint(limites.center);
            Quaternion rot = Quaternion.Euler(inclinacionCamara, 0f, 0f);
            Vector3 atras = rot * Vector3.forward;
            Vector3 ext = limites.extents;
            float lo = 1f, hi = 200f;
            for (int it = 0; it < 28; it++)
            {
                float d = (lo + hi) * 0.5f;
                cam.transform.position = centro - atras * d;
                cam.transform.rotation = rot;
                bool cabe = true;
                for (int k = 0; k < 8 && cabe; k++)
                {
                    Vector3 esq = centro + new Vector3((k & 1) == 0 ? -ext.x : ext.x, (k & 2) == 0 ? -ext.y : ext.y, (k & 4) == 0 ? -ext.z : ext.z);
                    Vector3 v = cam.WorldToViewportPoint(esq);
                    if (v.z <= 0f || v.x < margenCamara || v.x > 1f - margenCamara || v.y < margenCamara || v.y > 1f - margenCamara) cabe = false;
                }
                if (cabe) hi = d; else lo = d;
            }
            cam.transform.position = centro - atras * hi;
            cam.transform.rotation = rot;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = Mathf.Max(200f, hi * 3f);
        }

        // ================================================================== piezas de construcción

        private Material MaterialPorDefecto()
        {
            // se copia el material de una primitiva: funciona igual en Built-in, URP y WebGL
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            Destroy(tmp);
            return m;
        }

        private Transform Cubo(string nombre, Vector3 pos, Vector3 escala, Color color)
        {
            Material m;
            return Primitiva(PrimitiveType.Cube, nombre, pos, escala, color, out m);
        }

        private Transform Cubo(string nombre, Vector3 pos, Vector3 escala, Color color, out Material mat)
        {
            return Primitiva(PrimitiveType.Cube, nombre, pos, escala, color, out mat);
        }

        private Transform Primitiva(PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Color color, out Material mat, Transform padre = null)
        {
            GameObject go = GameObject.CreatePrimitive(tipo);
            go.name = nombre;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(padre != null ? padre : raiz, false);
            go.transform.localPosition = pos;
            go.transform.localScale = escala;
            mat = new Material(materialBase);
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private TextMeshPro Etiqueta(string texto, Vector3 pos, float tam, Color color, bool centro)
        {
            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(raiz, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.text = texto;
            t.fontSize = tam;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.richText = true;
            t.rectTransform.sizeDelta = new Vector2(centro ? 16f : 6f, 2f);
            etiquetas.Add(t);
            return t;
        }

        // ---- máquina con modelo 3D: la carcasa toma el color de la etapa; pantalla, baliza, brazo y engranaje se animan
        private void MaquinaConModelo(Maquina m, Vector3 pos)
        {
            Transform t = Instantiate(modeloMaquina, raiz).transform;
            Quaternion rm = t.localRotation;
            t.name = "Maquina M" + m.id;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, 180f, 0f) * rm;      // el panel mira a la cámara
            foreach (Collider c in t.GetComponentsInChildren<Collider>()) Destroy(c);
            m.cuerpo = t;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                string n = r.gameObject.name;
                if (n == "Pantalla") m.matPantalla = r.material;
                else if (n == "Baliza") m.matBaliza = r.material;
                else if (n == "Cuerpo")
                {
                    Material[] ms = r.materials;      // instancias propias de esta máquina
                    foreach (Material x in ms)
                        if (x != null && x.name.StartsWith("Acero") && !x.name.StartsWith("AceroOscuro")) m.material = x;
                    r.materials = ms;
                }
            }
            if (m.material != null) m.material.color = m.usada ? colorOciosa : colorSinUso;
            m.brazo = BuscarHijo(t, "Brazo");
            m.engranaje = BuscarHijo(t, "Engranaje");
            if (m.brazo != null) m.brazoBase = m.brazo.localRotation;
            if (m.engranaje != null) m.engranajeBase = m.engranaje.localRotation;
        }

        private void Cinta(Vector3 pos)
        {
            if (modeloCinta != null)
            {
                Transform t = Instantiate(modeloCinta, raiz).transform;
                Quaternion rm = t.localRotation;
                t.localPosition = pos;
                t.localRotation = Quaternion.Euler(0f, 180f, 0f) * rm;
                t.localScale = new Vector3(t.localScale.x * pasoMaquinas / 3.2f, t.localScale.y, t.localScale.z);
                foreach (Collider c in t.GetComponentsInChildren<Collider>()) Destroy(c);
                return;
            }
            Cubo("Cinta", pos + new Vector3(0f, 0.52f, 0f), new Vector3(pasoMaquinas, 0.06f, 0.7f), new Color(0.07f, 0.07f, 0.08f));
            Cubo("Riel", pos + new Vector3(0f, 0.52f, -0.38f), new Vector3(pasoMaquinas, 0.14f, 0.06f), new Color(0.95f, 0.72f, 0.08f));
            Cubo("Riel", pos + new Vector3(0f, 0.52f, 0.38f), new Vector3(pasoMaquinas, 0.14f, 0.06f), new Color(0.95f, 0.72f, 0.08f));
            Cubo("Pata", pos + new Vector3(-pasoMaquinas * 0.4f, 0.23f, 0f), new Vector3(0.08f, 0.46f, 0.66f), new Color(0.12f, 0.13f, 0.15f));
            Cubo("Pata", pos + new Vector3(pasoMaquinas * 0.4f, 0.23f, 0f), new Vector3(0.08f, 0.46f, 0.66f), new Color(0.12f, 0.13f, 0.15f));
        }

        /// <summary>Cambia el color de los renderers que se llaman "nombre" (p. ej. la franja "Color" de la pieza).</summary>
        private static void Teñir(Transform t, string nombre, Color c)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == nombre) r.material.color = c;
        }

        private static Transform BuscarHijo(Transform t, string nombre)
        {
            if (t.name == nombre) return t;
            foreach (Transform h in t)
            {
                Transform r = BuscarHijo(h, nombre);
                if (r != null) return r;
            }
            return null;
        }

        // ---- gatitos operadores (opcional): a la izquierda de su máquina, mirando a la cámara y un poco a la máquina

        private void CrearGato(Maquina m, Vector3 pos, int i)
        {
            Transform g;
            Quaternion rotModelo = Quaternion.identity;
            if (prefabGato != null)
            {
                g = Instantiate(prefabGato, raiz).transform;
                rotModelo = g.localRotation;
                g.name = "Gatito M" + m.id;
                g.localPosition = pos;
                foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
                Bounds b = new Bounds();
                bool hay = false;
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
                {
                    if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
                }
                if (hay && b.size.y > 0.001f)
                {
                    g.localScale *= alturaGato / b.size.y;
                    // apoyar las patas en el piso (el pivote del FBX puede estar en el centro)
                    float minY = float.MaxValue;
                    foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) minY = Mathf.Min(minY, r.bounds.min.y);
                    g.position += Vector3.up * (raiz.position.y - minY);
                }
            }
            else g = GatoPrimitivo(pos, i);
            m.gato = g;
            m.gatoBase = g.localPosition;
            m.gatoRot = rotModelo;
            g.localRotation = Quaternion.Euler(0f, giroGato, 0f) * rotModelo;
        }

        /// <summary>Gatito armado con primitivas (sin assets: liviano para WebGL).</summary>
        private Transform GatoPrimitivo(Vector3 pos, int i)
        {
            Color[] pelajes = { new Color(0.95f, 0.6f, 0.25f), new Color(0.55f, 0.57f, 0.62f), new Color(0.96f, 0.9f, 0.78f), new Color(0.25f, 0.25f, 0.28f) };
            Color c = pelajes[i % pelajes.Length];
            var g = new GameObject("Gatito " + i).transform;
            g.SetParent(raiz, false);
            g.localPosition = pos;
            float s = alturaGato;
            Material mat;
            Primitiva(PrimitiveType.Sphere, "Cuerpo", new Vector3(0f, 0.32f, 0f) * s, new Vector3(0.55f, 0.5f, 0.7f) * s, c, out mat, g);
            Primitiva(PrimitiveType.Sphere, "Cabeza", new Vector3(0f, 0.72f, 0.28f) * s, Vector3.one * 0.46f * s, c, out mat, g);
            Transform o1 = Primitiva(PrimitiveType.Cube, "Oreja I", new Vector3(-0.13f, 0.96f, 0.28f) * s, new Vector3(0.12f, 0.12f, 0.05f) * s, c, out mat, g);
            Transform o2 = Primitiva(PrimitiveType.Cube, "Oreja D", new Vector3(0.13f, 0.96f, 0.28f) * s, new Vector3(0.12f, 0.12f, 0.05f) * s, c, out mat, g);
            o1.localRotation = Quaternion.Euler(0f, 0f, 45f);
            o2.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Primitiva(PrimitiveType.Sphere, "Ojo I", new Vector3(-0.09f, 0.76f, 0.49f) * s, Vector3.one * 0.07f * s, Color.black, out mat, g);
            Primitiva(PrimitiveType.Sphere, "Ojo D", new Vector3(0.09f, 0.76f, 0.49f) * s, Vector3.one * 0.07f * s, Color.black, out mat, g);
            Primitiva(PrimitiveType.Sphere, "Nariz", new Vector3(0f, 0.69f, 0.51f) * s, Vector3.one * 0.05f * s, new Color(1f, 0.55f, 0.65f), out mat, g);
            Transform cola = Primitiva(PrimitiveType.Cylinder, "Cola", new Vector3(0f, 0.45f, -0.42f) * s, new Vector3(0.07f, 0.28f, 0.07f) * s, c, out mat, g);
            cola.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            return g;
        }
    }
}
