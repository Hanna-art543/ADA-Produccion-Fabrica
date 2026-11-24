# ADA-Produccion-Fabrica

Trabajo de Análisis y Diseño de Algoritmos: **Planificación de Producción en una Fábrica**.
Se resuelve con **Divide y Vencerás**, **Goloso** y **Programación Dinámica** por separado y con un
**método integrado** que combina los tres en un solo algoritmo. Todos se comparan contra una asignación
**aleatoria** y contra un **Branch & Bound** exacto (óptimo del escenario pequeño).

## 1. Problema

- Hay **piezas** que pasan, en orden, por tres etapas: **Corte → Ensamblaje → Empaquetado**.
  Cada par (pieza, etapa) es una **tarea** (`Job`) con `w_j` unidades de trabajo.
- Cada **máquina** `i` tiene una velocidad `v_i`, una potencia `P_i` (energía por unidad de tiempo) y la lista de etapas que puede hacer.
- Tiempo de proceso: `p_ij = w_j / v_i`. Energía: `e_ij = p_ij · P_i`.
- Costo total: **`C = α · makespan + β · energía`** (por defecto `α = 1` y `β = 0.1`).
- Restricción: **`energía ≤ E_max`**. Un plan que la excede se marca como **no factible**.
- Las máquinas rápidas gastan más energía por unidad de trabajo (`P ∝ v^1.6`), así que hay un trade-off real entre tiempo y energía.

**Objetivo:** maximizar la producción, es decir, terminar todas las piezas lo antes posible, con el menor costo y sin superar `E_max`.

### Simulación (`Simular`)

Todos los algoritmos deciden **qué máquina hace cada tarea**. Después, `Simulador.Simular` arma la línea de tiempo con una sola regla para todos (lista dinámica):

- En cada paso, entre las tareas listas (la siguiente etapa pendiente de cada pieza), se programa la que puede empezar antes.
- `inicio = max(fin de la etapa anterior de la pieza, instante en que la máquina queda libre)`.
- Si hay empate, va primero la pieza con más trabajo (LPT).

Así todos se comparan en el **mismo espacio de soluciones**, y el óptimo de Branch & Bound es una referencia válida para todos.

### Escenarios (semilla fija = 2024)

| # | Nombre  | Piezas | Tareas | Máquinas | E_max   |
|---|---------|-------:|-------:|---------:|--------:|
| 1 | Pequeño |      6 |     18 |        3 | 1047.87 |
| 2 | Mediano |     20 |     60 |        5 | 4470.64 |
| 3 | Grande  |     50 |    150 |        8 | 9176.58 |

- El trabajo por etapa es un entero en [10, 40] y la velocidad está en [1, 3].
- Cada etapa tiene al menos 2 máquinas capaces.
- `E_max = E_min + 0.35·(E_max_posible − E_min)`, así que la restricción sí se activa.

## 2. Estructura

```
Assets/Scripts/Core/            C# puro (namespace Fabrica.Core), sin UnityEngine
  Fabrica.Core.asmdef           noEngineReferences = true
  Modelo/                       Etapa, Job, Machine, Assignment, PlanResult, Escenario
  GeneradorEscenarios.cs        3 escenarios con semilla fija
  Simulador.cs                  Simular(plan): línea de tiempo, makespan, energía, costo, factible
  Rng.cs                        PRNG propio (mismos resultados en Unity y .NET)
  Algoritmos/
    Goloso.cs                   LPT + min(fin + λ·energía)
    CalibracionLambda.cs        búsqueda binaria de λ (energía lo más cerca de E_max sin pasarse)
    DivideVenceras.cs           por etapas + división recursiva de máquinas
    ProgramacionDinamica.cs     dp[i][e] con energía discretizada
    PlanificadorIntegrado.cs    Integrado: divide por etapas + menú goloso por etapa + PD sobre la energía
    Aleatorio.cs                1000 corridas: % factibles, promedio de factibles y mejor factible
    BranchAndBound.cs           exacto para n ≤ 8 piezas
  Experimentos/                 Experimento, Cotas, Comparador (tabla), ExportadorJson, ResumenComparativo
Assets/Scripts/Runner/
  FabricaRunner.cs              MonoBehaviour: ejecuta, Debug.Log de la tabla, exporta JSON (JsonUtility)
Tools/ConsoleTest/              Proyecto .NET 8 (C# 9) que compila Core y corre todo + verificaciones
Resultados/                     escenarioK_{greedy,dc,pd,int,random[,bb]}.json + resumen_comparativo.json
Assets/ADA_Fabrica/             Visualizador final en Unity: escena 3D + UI, Gantt y comparación (ver sección 8)
```

## 3. Cómo correrlo

**Unity**

1. Abre la carpeta del repo como proyecto en Unity (2021.2 o superior, por C# 9).
2. Crea un GameObject vacío y agrégale `FabricaRunner`.
3. Dale Play, o usa el menú contextual **"Ejecutar experimento"**.

La tabla aparece en la consola y los JSON se escriben en `Resultados/`. Los parámetros (`semilla`, `α`, `β`, `λ`, `E_max`, iteraciones, etc.) se editan en el inspector.

**Consola (sin Unity)**, requiere el .NET 8 SDK:

```
cd Tools/ConsoleTest
dotnet run -c Release
```

Imprime la tabla, exporta los JSON a `Resultados/` y corre las verificaciones:

- B&B coincide con una fuerza bruta de 2¹⁸ combinaciones.
- PD respeta `E_max`.
- El Integrado es factible y su costo es ≤ al mejor de Goloso, D&V y PD en los 3 escenarios (y se reporta su gap frente a B&B).
- Ningún algoritmo baja del óptimo ni de la cota.
- Se cumplen las precedencias y no hay solapes en las máquinas.

Termina con código 1 si alguna verificación falla.

## 4. Algoritmos

### Goloso (LPT)

1. Ordena las piezas por trabajo total, de mayor a menor (LPT).
2. Para cada pieza y cada etapa, elige la máquina válida que minimiza `fin + λ·e_ij`.
3. **Calibración de λ (búsqueda binaria, `k = 30` iteraciones):**
   - Con `λ = 0` solo importa el tiempo y se gasta la máxima energía. Si ese plan ya es factible, se usa.
   - Con `λ` muy grande, cada tarea va a su máquina de mínima energía, así que siempre es factible.
   - Se busca en escala logarítmica `λ = 10^x`, con `x ∈ [-4, 9]`, manteniendo factible el extremo alto. Se queda con el `λ` cuya energía total está **lo más cerca posible de `E_max` sin pasarse**.
   - Así el presupuesto de energía se reparte por igual entre todas las piezas, en vez de agotarse en las primeras.

El λ elegido queda en el campo `notas` del JSON.

### Divide y Vencerás

1. **Dividir por etapas:** Corte, Ensamblaje y Empaquetado se resuelven en orden. Los tiempos de liberación de cada etapa son los fines de la etapa anterior.
2. **Dentro de cada etapa** (recursivo):
   - Se divide el grupo de máquinas capaces en dos mitades de capacidad parecida (reparto en serpiente por velocidad).
   - Las tareas (en orden LPT) se reparten entre las dos mitades con el criterio goloso `(carga + w)/Σv + λ·w·min(P/v)`. El término de energía usa la máquina más eficiente del subgrupo; así, con λ grande, cada tarea baja hasta su máquina de mínima energía.
   - Se recurre en cada mitad. El caso base es una sola máquina.
3. **Combinar:** como las mitades no comparten máquinas, se unen las asignaciones y se secuencia la etapa según los tiempos de liberación.
4. **λ** se calibra con la misma búsqueda binaria del Goloso. Por eso el plan siempre es factible y no hace falta ningún paso de reparación.

### Programación Dinámica

- **`dp[i][e]`** = menor costo estimado al asignar las primeras `i` tareas (pieza por pieza en orden LPT) usando **exactamente `e` unidades de energía discreta**.
  - `ê_ij = ⌈e_ij · escala⌉`, con `escala = R / E_max` y resolución `R = 4000`.
- **Transición:** `dp[i+1][e + ê_ik] ← dp[i][e]` para cada máquina válida `k`, siempre que `e + ê_ik ≤ R`.
- **Reconstrucción:** `decision[i][e]` guarda la máquina elegida. Se recorre hacia atrás desde el mejor `e` final y la asignación resultante se secuencia con `Simular()`.

**Es exacta en energía y aproximada en makespan.** Por qué:

- La energía **es aditiva** (`E = Σ e_ij`), así que cabe exactamente en el índice de la tabla.
  - Como cada `ê` se redondea **hacia arriba**, `Σ ê ≤ R` implica `Σ e ≤ E_max`. La PD **nunca excede E_max**, y está verificado en los tres escenarios.
- El makespan **no es aditivo**: depende de cómo se reparten y solapan las tareas entre máquinas, es un `max` de cargas y no una suma.
  - Una PD exacta tendría que tener en el estado el vector completo de tiempos libres de las máquinas, y el número de estados crece exponencialmente.
  - Por eso cada celda `dp[i][e]` guarda **un solo** vector de cargas: el del camino de menor costo estimado.
  - Dos caminos con la misma energía y distinta distribución de carga se colapsan en uno, y ahí se pierde la exactitud.
- **Lo que se gana:**
  - Tiempo pseudo-polinomial `O(n·R·m²)` en vez de exponencial.
  - Factibilidad garantizada respecto a `E_max`.
  - Una visión **global** del presupuesto de energía, que el goloso no tiene.

**Evidencia** (escenario pequeño, comparado con el óptimo de B&B):

| Algoritmo         |  Costo | Gap vs óptimo |
|-------------------|-------:|--------------:|
| Branch & Bound    | 239.22 |        0.00 % |
| Divide y Vencerás | 247.97 |        3.66 % |
| Prog. Dinámica    | 260.82 |        9.03 % |
| Goloso (LPT)      | 271.85 |       13.64 % |

En los escenarios mediano y grande, la PD es el mejor de los tres métodos constructivos (Goloso, D&V y PD):

| Escenario | Goloso (costo) | D&V (costo) | PD (costo) |
|-----------|---------------:|------------:|-----------:|
| Mediano   |         718.64 |      673.89 |     645.83 |
| Grande    |        1818.70 |     1945.90 |    1706.37 |

El Goloso y D&V usan **un solo λ** para todas las tareas:

- Para no pasarse de `E_max` en el escenario grande, necesitan λ ≈ 20.
- Con ese peso, muchas tareas se van a máquinas lentas aunque no haga falta.
- La PD decide tarea por tarea cuánta energía usar y redistribuye el presupuesto donde más reduce el makespan.

### Integrado (D&V + Goloso + PD)

Es el método que pide el enunciado: **un solo algoritmo** que usa los tres paradigmas, cada uno en un paso.
Está en `Assets/Scripts/Core/Algoritmos/PlanificadorIntegrado.cs` (clave `int`).

1. **Divide (D&V):** el problema se parte en 3 subproblemas, uno por etapa (Corte, Ensamblaje, Empaquetado). Cada subproblema decide la máquina de las `P` tareas de esa etapa, solo entre las máquinas capaces. Las etapas van en orden: los fines de una etapa son los tiempos de liberación de la siguiente.
2. **Goloso:** para cada etapa se arma un **menú de opciones** (asignaciones parciales de esa etapa):
   - 17 pasadas golosas con distintos λ: `0`, `10⁶`, `λ_cal·{0.5, 1, 2}` (λ_cal = el λ calibrado del Goloso) y 12 valores en escala logarítmica de `0.01` a `1000`. Cada pasada toma las piezas por orden de liberación (desempate LPT) y manda cada tarea a la máquina que minimiza `fin + λ·energía`.
   - La parte de esa etapa de los planes completos **Goloso**, **D&V** y **PD**.
   - Se eliminan las opciones repetidas (misma máquina para cada pieza).
3. **PD (mochila de elección múltiple sobre la energía):** `dp[s][e]` = menor costo estimado `α·makespan + β·energía` tras las etapas `0..s` gastando exactamente `e` unidades de energía discreta (`R = 4000`, redondeo hacia arriba, así que nunca se pasa de `E_max`). Cada celda guarda el estado del mejor camino (máquinas libres y fin de cada pieza), y las opciones golosas de la etapa siguiente se calculan desde ese estado.
   - Como el makespan no es aditivo, la PD solo **estima**. Por eso se reconstruye la mejor combinación de **cada nivel de energía final** (hasta 200), y cada una se evalúa con el `Simulador` real, el mismo de todos los métodos.
   - Los **3 planes completos** Goloso, D&V y PD también se evalúan. Se elige la **factible de menor costo**.

**Por qué nunca es peor que los métodos solos:** los planes de Goloso, D&V y PD siempre están entre los candidatos, y todos se miden con el mismo `Simular()`. El elegido es el de menor costo, así que `costo(Integrado) ≤ min(Goloso, D&V, PD)`. ConsoleTest lo verifica en los 3 escenarios.

**Complejidad** (`S = 3` etapas, `L ≈ 20` opciones por etapa, `K ≤ 203` candidatas):

- Tiempo: `O(k·(n log n + n·m) + S·R·(P log P + L·P·m) + K·(R + n·P))`. Son la calibración de λ, la PD (R estados por etapa × L opciones × una pasada golosa) y la evaluación de las candidatas con `Simular()`. Es pseudo-polinomial en `R`.
- Espacio: `O(S·R·P + R·(m + P))`, por las decisiones por etapa y dos capas de estados.

El campo `notas` del JSON guarda las opciones por etapa, las combinaciones evaluadas y de dónde salió la opción elegida en cada etapa:

| Escenario | Corte            | Ensamblaje       | Empaquetado | Combinaciones evaluadas |
|-----------|------------------|------------------|-------------|------------------------:|
| Pequeño   | goloso λ = 3.687 | parte de D&V     | goloso λ = 0 | 201 |
| Mediano   | parte de PD      | parte de PD      | goloso λ = 0.6579 | 203 |
| Grande    | goloso λ = 43.29 | goloso λ = 5.337 | goloso λ = 10.2 | 202 |

En los tres escenarios el plan elegido es una **combinación nueva**: no es ninguno de los planes Goloso, D&V o PD completos.

### Aleatorio

Cada tarea va a una máquina válida al azar. Se hacen 1000 corridas con semilla fija y se reporta:

- **% factibles:** corridas con energía ≤ `E_max`.
- **Mejor factible:** la de menor costo entre las factibles.
- **Promedio de factibles:** makespan, energía y costo promedio, solo de las corridas factibles.

Si ninguna corrida es factible, la tabla muestra **"sin solución factible"**. En ese caso el JSON queda con `hayFactible = false` y sin asignaciones.

Campos del JSON: `hayFactible`, `corridas`, `corridasFactibles`, `porcentajeFactibles`, `makespanPromedioFactibles`, `energiaPromedioFactibles` y `costoPromedioFactibles`.

### Branch & Bound (método extra, exacto)

Búsqueda en profundidad sobre la máquina de cada tarea. La solución del goloso sirve como cota superior inicial. Cada nodo usa estas cotas inferiores:

- `makespan ≥ max(carga de cada máquina, cadena de cada pieza (asignado + tiempo mínimo pendiente), (tiempo total asignado + mínimo restante) / M)`.
- `energía ≥ asignada + mínima restante`.

Se poda si `α·LB_mk + β·LB_e ≥ mejor` o si `LB_e > E_max`. Cada hoja se evalúa con `Simular()`. Solo se usa con 8 piezas o menos, y su resultado es el óptimo de referencia del escenario pequeño, comprobado contra la fuerza bruta.

## 5. Complejidades

`n` = tareas (3 × piezas), `m` = máquinas, `P` = piezas, `k` = iteraciones de la búsqueda binaria de λ (30), `R` = resolución de energía de la PD, `L` = opciones por etapa del Integrado, `C` = combinaciones que el Integrado evalúa y `K` = corridas del aleatorio. Una llamada a `Simular()` cuesta `O(n·P)`.

| Algoritmo           | Tiempo                                | Espacio      | Comentario |
|---------------------|---------------------------------------|--------------|------------|
| Simular             | O(n·P)                                | O(n + m)     | En cada paso elige entre ≤ P tareas listas |
| Goloso (LPT)        | O(k·(n log n + n·m))                  | O(n + m)     | k pasadas golosas (orden LPT + evaluar m máquinas por tarea), una por cada λ de la búsqueda binaria |
| Divide y Vencerás   | O(k·(n log n + n·m))                  | O(n + m)     | Cada pasada: T(n, m) = T(n₁, m/2) + T(n₂, m/2) + O(n + m), o sea profundidad log m con O(n + m) por nivel, más el orden O(n log n). Se repite k veces por la calibración de λ |
| Prog. Dinámica      | O(n·R·m²)                             | O(n·R + R·m) | n·R estados × m transiciones × copia del vector de m cargas. Pseudo-polinomial |
| Integrado           | O(k·(n log n + n·m) + S·R·(P log P + L·P·m) + C·(R + n·P)) | O(S·R·P + R·(m + P)) | Calibración de λ + PD sobre 3 etapas con L opciones golosas + C simulaciones. Pseudo-polinomial |
| Aleatorio           | O(K·n·P)                              | O(n)         | K simulaciones |
| Branch & Bound      | O(mⁿ·n·P) peor caso                   | O(n + m)     | En la práctica, la poda explora muy pocos nodos (ver `notas` en el JSON) |

## 6. Resultados (semilla 2024)

```
=== Escenario 1: Pequeño (6 piezas, 18 tareas, 3 máquinas) | E_max = 1047.87 | α = 1 β = 0.1 ===
Referencia: óptimo de Branch & Bound, costo = 239.22
Algoritmo                      |  Makespan |   Energía |     Costo |  Gap vs ópt (%) | Tiempo (ms) | Factible
-------------------------------------------------------------------------------------------------------------
Goloso (LPT)                   |    167.81 |   1040.45 |    271.85 |           13.64 |       2.326 | sí
Divide y Vencerás              |    143.20 |   1047.71 |    247.97 |            3.66 |       3.654 | sí
Prog. Dinámica                 |    156.58 |   1042.46 |    260.82 |            9.03 |      12.143 | sí
Integrado (D&V + Goloso + PD)  |    143.37 |   1034.16 |    246.79 |            3.16 |      59.672 | sí
Aleatorio (mejor factible)     |    147.65 |   1037.80 |    251.43 |            5.10 |      11.904 | sí
Aleatorio (prom. factibles)    |    199.11 |   1027.08 |    301.82 |           26.17 |      11.904 | sí
Branch & Bound                 |    134.76 |   1044.56 |    239.22 |            0.00 |      16.049 | sí
Aleatorio: 13.2 % factibles (132 de 1000 corridas).

=== Escenario 2: Mediano (20 piezas, 60 tareas, 5 máquinas) | E_max = 4470.64 | α = 1 β = 0.1 ===
Referencia: COTA INFERIOR (no es el óptimo), costo ≥ 544.61
Algoritmo                      |  Makespan |   Energía |     Costo | Gap vs cota (%) | Tiempo (ms) | Factible
-------------------------------------------------------------------------------------------------------------
Goloso (LPT)                   |    272.26 |   4463.77 |    718.64 |           31.96 |       0.389 | sí
Divide y Vencerás              |    227.06 |   4468.24 |    673.89 |           23.74 |       0.796 | sí
Prog. Dinámica                 |    202.23 |   4436.04 |    645.83 |           18.59 |       5.987 | sí
Integrado (D&V + Goloso + PD)  |    188.55 |   4457.95 |    634.35 |           16.48 |      26.490 | sí
Aleatorio (mejor factible)     |    245.29 |   4452.92 |    690.59 |           26.80 |       7.692 | sí
Aleatorio (prom. factibles)    |    311.78 |   4458.65 |    757.65 |           39.12 |       7.692 | sí
Aleatorio: 2.3 % factibles (23 de 1000 corridas).

=== Escenario 3: Grande (50 piezas, 150 tareas, 8 máquinas) | E_max = 9176.58 | α = 1 β = 0.1 ===
Referencia: COTA INFERIOR (no es el óptimo), costo ≥ 1037.41
Algoritmo                      |  Makespan |   Energía |     Costo | Gap vs cota (%) | Tiempo (ms) | Factible
-------------------------------------------------------------------------------------------------------------
Goloso (LPT)                   |    902.45 |   9162.46 |   1818.70 |           75.31 |       2.723 | sí
Divide y Vencerás              |   1031.05 |   9148.41 |   1945.90 |           87.57 |       2.377 | sí
Prog. Dinámica                 |    806.85 |   8995.12 |   1706.37 |           64.48 |      25.045 | sí
Integrado (D&V + Goloso + PD)  |    766.50 |   9123.18 |   1678.82 |           61.83 |     130.383 | sí
Aleatorio                      | sin solución factible                               |      51.336 | -
Aleatorio: 0.0 % factibles (0 de 1000 corridas).
```

Notas sobre la tabla:

- En los escenarios 2 y 3, el gap se mide contra una **cota inferior** que no tiene por qué ser alcanzable. Por eso el gap real es menor que el reportado.
- El Goloso supera al promedio de las corridas aleatorias factibles en los escenarios donde ese promedio existe (271.85 < 301.82 y 718.64 < 757.65).
- En el escenario grande, al azar no se encuentra ningún plan que respete `E_max`. El Goloso calibrado sí lo encuentra.
- El **Integrado** es el mejor de los métodos constructivos en los 3 escenarios: 246.79 < 247.97 (D&V), 634.35 < 645.83 (PD) y 1678.82 < 1706.37 (PD). En el escenario pequeño queda a 3.16 % del óptimo de B&B.
- Su tiempo incluye el de Goloso, D&V y PD, porque los usa como entrada.

Los tiempos de ejecución (ms) están en la salida de consola y en `Resultados/resumen_comparativo.json`.

## 7. Escena de simulación en Unity (Persona 3)

La escena se construye **por código**: no hay prefabs ni objetos que armar a mano.

```
Assets/Scripts/Escena/
  CargadorResultados.cs     Lee escenarioK_<metodo>.json de StreamingAssets/Resultados (File o UnityWebRequest en WebGL)
  VisualizadorFabrica.cs    Dibuja y anima un PlanResult: CargarPlan(PlanResult), Play, Pausa, Reiniciar, FijarVelocidad
  HudFabrica.cs             Controles IMGUI opcionales (play, pausa, reiniciar, tiempo, velocidad, escenario y método)
Assets/Editor/
  CrearEscenaFabrica.cs     Menú Fabrica > Persona 3: crea la escena y copia los JSON
Assets/StreamingAssets/Resultados/   Copia de los JSON (la regenera FabricaRunner en el Editor)
```

**Cómo probarla**

1. Abre el proyecto en Unity y usa el menú **Fabrica > Persona 3 > Crear escena de simulación**.
2. Dale Play. Con el HUD cambias de escenario, de método, de velocidad y pausas o reinicias.
   Atajos de teclado: `Espacio` pausa, `R` reinicia, `↑`/`↓` duplican o reducen la velocidad.

**Cómo la usa la Persona 4**

- Agregar `VisualizadorFabrica` a un GameObject de su escena, desmarcar `cargarAlIniciar` y desmarcar `visible` en `HudFabrica` (o quitarlo).
- Llamar a `visualizador.CargarPlan(plan)` con un `PlanResult`, o a `visualizador.CargarEscenario(1, "pd")`.
- Leer `Tiempo`, `EnergiaAcumulada`, `FraccionEnergia`, `PiezasTerminadas` y los eventos `PlanCargado` y `Terminado`.
- `CargadorResultados.CargarResumen(...)` entrega `resumen_comparativo.json` para el gráfico de barras.

## 8. Visualizador final en Unity (escena 3D + UI)

Es la versión que se presenta y se publica en WebGL. Une la escena de la fábrica con la interfaz (selectores, Gantt, gráfico comparativo y botón Comparar). Todo está en `Assets/ADA_Fabrica/` y no modifica el Core ni los archivos de las secciones anteriores.

```
Assets/ADA_Fabrica/
  Scripts/Datos/        RepositorioPlanes (único lector: Resources + JsonUtility, válido en WebGL),
                        ControladorPlan (CargarPlan(escenario, clave) → evento OnPlanCargado), CalculosFabrica
  Scripts/Reloj/        IRelojSimulacion / IRelojControlable, RelojSimple (solo escena de prueba)
  Scripts/Escena3D/     VisualizadorFabrica (línea de N máquinas, piezas, operarios, energía; es el reloj único),
                        TiempoPiezas (estado de cada pieza en el instante t), AmbienteFabrica, LayoutAmbiente
  Scripts/UI/           PanelSeleccion, GanttChart, GraficoComparativo, PanelComparar, ControlesReloj, TooltipUI...
  Editor/               Menú "ADA Fabrica": construye el prefab de la UI, la escena final, la verificación y el build WebGL
  Modelos/              17 FBX livianos (máquina, cinta, piezas por etapa, operarios, nave), ~850 KB
  Resources/Resultados/ Copia de los 17 JSON de Resultados/ (desde aquí los lee el visualizador)
```

Clases en los namespaces `Fabrica.UI` y `Fabrica.Escena`, así que conviven con la escena de la sección 7 sin chocar.

**Cómo abrirlo**

1. Abre la carpeta del repo en **Unity 6** (Unity Hub → Add → Add project from disk). La primera vez Unity genera `ProjectSettings/`, `Packages/` y los `.meta`.
2. Si lo pide: **Window → TextMeshPro → Import TMP Essential Resources**.
3. Menú **ADA Fabrica → 3. Construir escena FINAL ADA_Fabrica (única del build)**. Crea `Assets/ADA_Fabrica/Scenes/ADA_Fabrica.unity` y el prefab `ADA_UI`, la deja como única escena del build y corre la verificación (en consola: `[ADA] Verificación ADA: TODO OK`).
4. Dale Play.

Si se regeneran los JSON con `FabricaRunner` o con la consola, cópialos con **ADA Fabrica → 1. Importar Resultados (JSON) desde el repo...**.

**Controles:** escenario y método; Play/Pausa, Reiniciar y velocidad (x0.5 a x8); clic en el Gantt para saltar a ese tiempo; al pasar el mouse por una barra se ven los datos de la tarea; **Comparar con Aleatorio** da el % de mejora frente al mejor plan aleatorio y frente al promedio.

**Decisiones de diseño**

- El número de máquinas sale de `resumen_comparativo.json` (no del mayor `maquinaId`): la PD deja sin uso la máquina 1 en el escenario 2 y las máquinas 0 y 7 en el escenario 3, y igual se muestran.
- Una máquina puede hacer varias etapas: se dibuja una línea de N máquinas cuyo color indica la etapa que procesa (gris = ociosa), igual que en el Gantt.
- Un solo reloj (`VisualizadorFabrica`) para la animación y el cursor del Gantt, en t ∈ [0, makespan].
- Energía acumulada: E(t) = Σ e_j · clamp((t − inicio_j)/(fin_j − inicio_j), 0, 1), en rojo desde el 90 % de E_max.
- `escenario3_random.json` viene vacío (ninguna corrida factible): se muestra "sin solución factible".
- Claves de los planes, en el orden de la UI: `greedy` (Goloso), `dc` (D&V), `pd` (PD), `int` (Integrado (D&V + Goloso + PD), corto "Integrado"), `random` (Aleatorio) y `bb` (B&B, solo escenario 1). Son 16 planes: 5 por escenario + B&B. `CalculosFabrica.ClaveDeAlgoritmo` detecta "integr"/"hibrid" antes que "golos", porque el nombre del Integrado contiene "Goloso".

**Verificación:** 101 comprobaciones con los JSON reales (makespan, energía y costo de cada plan, máquinas sin uso, piezas en la salida al terminar, energía final = `energiaTotal`, Comparar = `resumen_comparativo.json`). El menú **ADA Fabrica → 4. Verificar escena abierta y datos reales** las repite dentro de Unity.

**Build WebGL para GitHub Pages**

1. **ADA Fabrica → WebGL: aplicar configuración para GitHub Pages (sin compresión)**. Pages no envía las cabeceras que necesita Brotli.
2. **ADA Fabrica → WebGL: build a la carpeta docs/ (GitHub Pages)**. Crea `docs/` con `.nojekyll`.
3. Sube `docs/` y en GitHub: **Settings → Pages → Branch: main, carpeta /docs**.
