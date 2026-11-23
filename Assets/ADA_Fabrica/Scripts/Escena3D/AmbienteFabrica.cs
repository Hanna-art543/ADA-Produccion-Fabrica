using System.Collections.Generic;
using Fabrica.Core;
using UnityEngine;

namespace Fabrica.Escena
{
    /// <summary>
    /// Ambientación de la nave: muros con ventanas, columnas, estanterías, palés, tanques, lámparas
    /// y gatitos trabajando al fondo (montacargas, cargando cajas, empujando un carrito, inspeccionando).
    /// Es solo decoración: no afecta a los datos ni al reloj. Se rehace cuando cambia el tamaño de la fábrica.
    /// Si falta un modelo, usa una caja gris en su lugar (nunca rompe la escena).
    /// </summary>
    [RequireComponent(typeof(VisualizadorFabrica))]
    public class AmbienteFabrica : MonoBehaviour
    {
        [System.Serializable]
        public class Modelo
        {
            public string nombre;
            public GameObject prefab;
        }

        [Tooltip("Modelos por nombre: MuroVentana, Columna, Estanteria, Palet, Tanque, CajaCarton, MesaControl, Lampara, Montacargas, Carrito, GatoObrero")]
        public List<Modelo> modelos = new List<Modelo>();
        public bool mostrar = true;
        public float alturaGatito = 0.85f;
        public Color colorPiso = new Color(0.33f, 0.34f, 0.36f);

        private VisualizadorFabrica vis;
        private Transform raiz;
        private float mitadPrevia = -1f;
        private readonly List<Activo> activos = new List<Activo>();
        private Material matBase;

        private static readonly Color[] Pelajes =
        {
            new Color(1f, 1f, 1f), new Color(0.75f, 0.78f, 0.85f), new Color(1.1f, 1.05f, 0.95f), new Color(0.55f, 0.55f, 0.6f)
        };

        private class Activo
        {
            public LayoutAmbiente.Trabajador t;
            public Transform cuerpo;     // gatito (o montacargas con su gatito)
            public Transform gato;
            public Transform carga;
            public float yBase;
        }

        private void Awake()
        {
            vis = GetComponent<VisualizadorFabrica>();
        }

        private void OnEnable()
        {
            if (vis == null) vis = GetComponent<VisualizadorFabrica>();
            vis.FabricaConstruida += AlConstruir;
        }

        private void OnDisable()
        {
            if (vis != null) vis.FabricaConstruida -= AlConstruir;
        }

        private void AlConstruir(float mitadLinea, float zFrente, float zMedidor)
        {
            if (!mostrar) return;
            if (raiz != null && Mathf.Abs(mitadLinea - mitadPrevia) < 0.01f) return;   // mismo tamaño: se conserva
            mitadPrevia = mitadLinea;
            if (raiz != null) Destroy(raiz.gameObject);
            activos.Clear();
            raiz = new GameObject("Ambiente").transform;
            raiz.SetParent(transform, false);

            LayoutAmbiente.Resultado lay = LayoutAmbiente.Calcular(mitadLinea, zFrente, zMedidor);

            // piso grande con líneas de seguridad
            Bloque("Piso nave", new Vector3((lay.xMin + lay.xMax) * 0.5f, -0.07f, (lay.zFrente + lay.zFondo) * 0.5f),
                   new Vector3(lay.xMax - lay.xMin + 1f, 0.1f, lay.zFondo - lay.zFrente + 1f), colorPiso);
            Color amarillo = new Color(0.95f, 0.75f, 0.1f);
            float zP = zMedidor + 1.6f;
            Bloque("Linea", new Vector3(0f, -0.015f, zP - 1.15f), new Vector3(lay.xMax - lay.xMin - 3f, 0.01f, 0.1f), amarillo);
            Bloque("Linea", new Vector3(0f, -0.015f, zP + 1.15f), new Vector3(lay.xMax - lay.xMin - 3f, 0.01f, 0.1f), amarillo);

            foreach (LayoutAmbiente.Item it in lay.items)
                Instanciar(it.modelo, new Vector3(it.x, it.y, it.z), it.giro, it.escala);

            // cables del medidor colgante
            foreach (float x in new[] { -2.5f, 2.5f })
                Bloque("Cable", new Vector3(x, 4.2f, zMedidor), new Vector3(0.03f, 2.2f, 0.03f), new Color(0.1f, 0.1f, 0.1f));

            for (int i = 0; i < lay.trabajadores.Count; i++) CrearTrabajador(lay.trabajadores[i]);
        }

        // ------------------------------------------------------------------ trabajadores

        private void CrearTrabajador(LayoutAmbiente.Trabajador t)
        {
            var a = new Activo { t = t };
            Transform gato = Gatito(t.pelaje);
            switch (t.actividad)
            {
                case LayoutAmbiente.Actividad.Montacargas:
                    {
                        Transform m = Instanciar("Montacargas", Vector3.zero, 0f, 1f);
                        Transform asiento = Buscar(m, "Asiento");
                        gato.SetParent(asiento != null ? asiento : m, false);
                        gato.localPosition = asiento != null ? Vector3.zero : new Vector3(0f, 0.9f, 0f);
                        gato.localRotation = Quaternion.identity;
                        gato.localScale = Vector3.one * 0.8f;
                        a.cuerpo = m;
                        break;
                    }
                case LayoutAmbiente.Actividad.EmpujarCarrito:
                    {
                        Transform cuerpo = new GameObject("Gatito con carrito").transform;
                        cuerpo.SetParent(raiz, false);
                        Transform c = Instanciar("Carrito", Vector3.zero, 0f, 1f);
                        c.SetParent(cuerpo, false);
                        c.localPosition = new Vector3(0f, 0f, 0.75f);
                        c.localRotation = Quaternion.identity;   // el asa del carrito queda hacia el gatito
                        gato.SetParent(cuerpo, false);
                        gato.localPosition = Vector3.zero;
                        a.cuerpo = cuerpo;
                        break;
                    }
                default:
                    a.cuerpo = gato;
                    if (t.actividad == LayoutAmbiente.Actividad.CargarCaja)
                    {
                        a.carga = Instanciar("CajaCarton", Vector3.zero, 0f, 0.8f);
                        a.carga.SetParent(gato, true);
                        a.carga.localPosition = new Vector3(0f, alturaGatito * 0.45f, 0.42f);
                        a.carga.localRotation = Quaternion.identity;
                    }
                    break;
            }
            a.gato = gato;
            a.yBase = 0f;
            activos.Add(a);
        }

        private void Update()
        {
            float tiempo = Time.time;
            foreach (Activo a in activos)
            {
                if (a.cuerpo == null) continue;
                float x, z, giro, paso;
                LayoutAmbiente.Pose(a.t, tiempo, out x, out z, out giro, out paso);
                float y = 0f;
                switch (a.t.actividad)
                {
                    case LayoutAmbiente.Actividad.Inspeccionar:
                        giro = 180f + 25f * Mathf.Sin(tiempo * 0.9f);
                        y = 0.02f * Mathf.Abs(Mathf.Sin(tiempo * 3f));
                        break;
                    case LayoutAmbiente.Actividad.Descansar:
                        giro = 200f;
                        y = 0.015f * Mathf.Sin(tiempo * 1.5f);
                        break;
                    case LayoutAmbiente.Actividad.Saludar:
                        giro = 160f + 20f * Mathf.Sin(tiempo * 4f);
                        y = 0.05f * Mathf.Abs(Mathf.Sin(tiempo * 4f));
                        break;
                    case LayoutAmbiente.Actividad.Montacargas:
                        break;
                    default:
                        y = paso * 0.05f * Mathf.Abs(Mathf.Sin(tiempo * 9f + a.t.desfase));   // saltitos al caminar
                        break;
                }
                a.cuerpo.localPosition = new Vector3(x, a.yBase + y, z);
                a.cuerpo.localRotation = Quaternion.Euler(0f, giro, 0f) * Quaternion.identity;
                // la caja solo se lleva de ida (de la estantería a la mesa)
                if (a.carga != null) a.carga.gameObject.SetActive(giro > 0f || paso == 0f);
            }
        }

        // ------------------------------------------------------------------ utilidades

        private GameObject Prefab(string nombre)
        {
            foreach (Modelo m in modelos) if (m != null && m.nombre == nombre) return m.prefab;
            return null;
        }

        /// <summary>Instancia el modelo con su frente girado "giro" grados desde +z. Sin modelo: caja de respaldo.</summary>
        private Transform Instanciar(string nombre, Vector3 pos, float giro, float escala)
        {
            GameObject p = Prefab(nombre);
            Transform t;
            if (p != null)
            {
                t = Instantiate(p, raiz).transform;
                Quaternion rotModelo = t.localRotation;
                t.localPosition = pos;
                t.localRotation = Quaternion.Euler(0f, giro, 0f) * rotModelo;
                t.localScale = t.localScale * escala;
                foreach (Collider c in t.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                Vector3 tam = TamRespaldo(nombre) * escala;
                t = Bloque(nombre + " (sin modelo)", pos + Vector3.up * tam.y * 0.5f, tam, new Color(0.45f, 0.47f, 0.5f));
                t.localRotation = Quaternion.Euler(0f, giro, 0f);
            }
            t.name = nombre;
            return t;
        }

        private static Vector3 TamRespaldo(string n)
        {
            switch (n)
            {
                case "MuroVentana": return new Vector3(4f, 5f, 0.3f);
                case "Columna": return new Vector3(0.3f, 5f, 0.3f);
                case "Estanteria": return new Vector3(2.6f, 2.7f, 0.9f);
                case "Palet": return new Vector3(1.2f, 1f, 1f);
                case "Tanque": return new Vector3(1.5f, 2.6f, 1.5f);
                case "Montacargas": return new Vector3(1f, 2.1f, 2f);
                case "Lampara": return new Vector3(0.6f, 0.2f, 0.6f);
                default: return new Vector3(0.5f, 0.5f, 0.5f);
            }
        }

        private Transform Gatito(int pelaje)
        {
            GameObject p = Prefab("GatoObrero");
            if (p == null) p = vis.prefabGato;
            Transform g;
            if (p != null)
            {
                g = Instantiate(p, raiz).transform;
                Quaternion rotModelo = g.localRotation;
                foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
                // normalizar altura y apoyar en el piso: se envuelve en un objeto para poder girarlo libremente
                var envoltura = new GameObject("Gatito").transform;
                envoltura.SetParent(raiz, false);
                g.SetParent(envoltura, false);
                g.localRotation = rotModelo;
                Bounds b = new Bounds(); bool hay = false;
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
                if (hay && b.size.y > 0.001f)
                {
                    g.localScale *= alturaGatito / b.size.y;
                    float minY = float.MaxValue;
                    foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) minY = Mathf.Min(minY, r.bounds.min.y);
                    g.position += Vector3.up * (envoltura.position.y - minY);
                }
                Teñir(g, Pelajes[pelaje % Pelajes.Length]);
                return envoltura;
            }
            // sin modelo: caja con "orejas" (respaldo mínimo)
            Transform s = Bloque("Gatito (sin modelo)", new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.5f), new Color(0.95f, 0.6f, 0.25f));
            return s;
        }

        /// <summary>Multiplica el color del pelaje para variar a los gatitos.</summary>
        private static void Teñir(Transform g, Color c)
        {
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            {
                Material[] ms = r.materials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && ms[i].name.StartsWith("GatoPelaje")) ms[i].color = ms[i].color * c;
                r.materials = ms;
            }
        }

        private static Transform Buscar(Transform t, string nombre)
        {
            if (t.name == nombre) return t;
            foreach (Transform h in t)
            {
                Transform r = Buscar(h, nombre);
                if (r != null) return r;
            }
            return null;
        }

        private Transform Bloque(string nombre, Vector3 pos, Vector3 escala, Color color)
        {
            if (matBase == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                matBase = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                Destroy(tmp);
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(raiz, false);
            go.transform.localPosition = pos;
            go.transform.localScale = escala;
            var m = new Material(matBase) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }
    }
}
