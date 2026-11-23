using System;
using System.Collections.Generic;

namespace Fabrica.Escena
{
    /// <summary>
    /// Distribución de la nave alrededor de la línea de máquinas (SIN UnityEngine: se puede exportar y previsualizar).
    /// Coordenadas de Unity: x a la derecha, y arriba, z hacia el fondo (la cámara mira desde −z).
    /// giro = ángulo Y en grados (0 = el frente del modelo mira a +z, 180 = mira a la cámara).
    /// </summary>
    public static class LayoutAmbiente
    {
        public struct Item
        {
            public string modelo;
            public float x, y, z, giro, escala;
            public Item(string m, float x, float y, float z, float giro, float escala = 1f)
            { modelo = m; this.x = x; this.y = y; this.z = z; this.giro = giro; this.escala = escala; }
        }

        public enum Actividad { Montacargas, CargarCaja, EmpujarCarrito, Inspeccionar, Descansar, Saludar }

        /// <summary>Gatito de fondo: va y viene entre A y B (o se queda quieto si A = B).</summary>
        public struct Trabajador
        {
            public Actividad actividad;
            public float ax, az, bx, bz;
            public float velocidad;     // m/s
            public float desfase;       // s
            public int pelaje;
        }

        public class Resultado
        {
            public List<Item> items = new List<Item>();
            public List<Trabajador> trabajadores = new List<Trabajador>();
            public float xMin, xMax, zFrente, zFondo, altoTecho;
        }

        /// <param name="mitadLinea">media longitud de la línea de máquinas</param>
        /// <param name="zFrenteFabrica">borde delantero de la zona de colas</param>
        /// <param name="zMedidor">z del medidor colgante</param>
        public static Resultado Calcular(float mitadLinea, float zFrenteFabrica, float zMedidor)
        {
            var r = new Resultado();
            float ancho = Math.Max(mitadLinea + 6.5f, 11f);       // media anchura de la nave
            float zPasillo = zMedidor + 1.6f;                      // pasillo trasero (montacargas)
            float zEstantes = zPasillo + 2.3f;
            float zMuro = zEstantes + 1.0f;
            r.xMin = -ancho; r.xMax = ancho; r.zFrente = zFrenteFabrica - 3f; r.zFondo = zMuro; r.altoTecho = 5f;

            // ---- muro del fondo con ventanas (segmentos de 4 m) y columnas
            int segs = (int)Math.Ceiling(ancho * 2 / 4f);
            float x0 = -segs * 2f + 2f;
            for (int i = 0; i < segs; i++) r.items.Add(new Item("MuroVentana", x0 + i * 4f, 0f, zMuro, 180f));
            for (int i = 0; i <= segs; i += 2) r.items.Add(new Item("Columna", x0 - 2f + i * 4f, 0f, zMuro - 0.35f, 180f));
            // muros laterales (parciales, hacia el frente)
            for (float z = zMuro - 2f; z > r.zFrente + 2f; z -= 4f)
            {
                r.items.Add(new Item("MuroVentana", -ancho - 0.2f, 0f, z, 90f));
                r.items.Add(new Item("MuroVentana", ancho + 0.2f, 0f, z, -90f));
            }

            // ---- estanterías contra el muro, palés y tanques
            float xe = -ancho + 2.2f;
            int k = 0;
            while (xe < ancho - 2f)
            {
                if (k % 3 == 2) r.items.Add(new Item("Palet", xe, 0f, zEstantes, 180f + (k * 37 % 20) - 10f));
                else r.items.Add(new Item("Estanteria", xe, 0f, zEstantes, 180f));
                xe += 3.1f;
                k++;
            }
            r.items.Add(new Item("Tanque", -ancho + 1.4f, 0f, zPasillo - 0.6f, 180f));
            r.items.Add(new Item("Tanque", ancho - 1.4f, 0f, zPasillo - 0.6f, 180f));
            r.items.Add(new Item("Palet", -ancho + 1.5f, 0f, zFrenteFabrica + 0.2f, 160f));
            r.items.Add(new Item("Palet", ancho - 1.5f, 0f, zFrenteFabrica + 0.2f, 200f));
            r.items.Add(new Item("CajaCarton", -ancho + 2.6f, 0f, zFrenteFabrica - 0.4f, 170f));
            r.items.Add(new Item("CajaCarton", ancho - 2.7f, 0f, zFrenteFabrica - 0.2f, 195f));
            r.items.Add(new Item("MesaControl", mitadLinea + 2.4f, 0f, 0.6f, 200f));

            // ---- lámparas colgantes sobre la línea
            for (float x = -mitadLinea + 1.6f; x <= mitadLinea - 1.0f; x += 3.2f) r.items.Add(new Item("Lampara", x, 3.9f, 0.3f, 0f));

            // ---- gatitos trabajando al fondo
            r.trabajadores.Add(new Trabajador { actividad = Actividad.Montacargas, ax = -ancho + 3.5f, az = zPasillo, bx = ancho - 3.5f, bz = zPasillo, velocidad = 1.6f, pelaje = 0 });
            r.trabajadores.Add(new Trabajador { actividad = Actividad.CargarCaja, ax = -mitadLinea * 0.6f, az = zEstantes - 1.1f, bx = -mitadLinea * 0.05f, bz = zEstantes - 1.1f, velocidad = 0.9f, desfase = 1.3f, pelaje = 1 });
            r.trabajadores.Add(new Trabajador { actividad = Actividad.CargarCaja, ax = mitadLinea * 0.15f, az = zEstantes - 1.0f, bx = mitadLinea * 0.7f, bz = zEstantes - 1.0f, velocidad = 0.8f, desfase = 4.1f, pelaje = 2 });
            r.trabajadores.Add(new Trabajador { actividad = Actividad.EmpujarCarrito, ax = -ancho + 1.6f, az = zFrenteFabrica + 1.4f, bx = -ancho + 1.6f, bz = zPasillo - 2.2f, velocidad = 0.7f, desfase = 0.5f, pelaje = 3 });
            r.trabajadores.Add(new Trabajador { actividad = Actividad.Inspeccionar, ax = mitadLinea + 2.4f, az = 1.35f, bx = mitadLinea + 2.4f, bz = 1.35f, pelaje = 1 });
            r.trabajadores.Add(new Trabajador { actividad = Actividad.Descansar, ax = ancho - 1.5f, az = zFrenteFabrica + 1.2f, bx = ancho - 1.5f, bz = zFrenteFabrica + 1.2f, pelaje = 2 });
            r.trabajadores.Add(new Trabajador { actividad = Actividad.Saludar, ax = -mitadLinea - 2.2f, az = zFrenteFabrica + 0.4f, bx = -mitadLinea - 2.2f, bz = zFrenteFabrica + 0.4f, pelaje = 0 });
            return r;
        }

        /// <summary>Posición (x, z), rumbo (giro Y) y si va cargado, para un trabajador en el instante t (s).</summary>
        public static void Pose(Trabajador w, float t, out float x, out float z, out float giro, out float paso)
        {
            float dx = w.bx - w.ax, dz = w.bz - w.az;
            float largo = (float)Math.Sqrt(dx * dx + dz * dz);
            if (largo < 0.01f || w.velocidad <= 0f)
            {
                x = w.ax; z = w.az; giro = 180f; paso = 0f;
                return;
            }
            float pausa = 1.2f;                               // se detiene en cada extremo (carga / descarga)
            float tramo = largo / w.velocidad;
            float ciclo = 2f * (tramo + pausa);
            float u = (t + w.desfase) % ciclo;
            if (u < 0) u += ciclo;
            float f; bool ida;
            if (u < tramo) { f = u / tramo; ida = true; paso = 1f; }
            else if (u < tramo + pausa) { f = 1f; ida = false; paso = 0f; }
            else if (u < 2f * tramo + pausa) { f = 1f - (u - tramo - pausa) / tramo; ida = false; paso = 1f; }
            else { f = 0f; ida = true; paso = 0f; }
            f = f * f * (3f - 2f * f);
            x = w.ax + dx * f; z = w.az + dz * f;
            float hx = ida ? dx : -dx, hz = ida ? dz : -dz;
            giro = (float)(Math.Atan2(hx, hz) * 180.0 / Math.PI);  // 0 = mira a +z
        }
    }
}
