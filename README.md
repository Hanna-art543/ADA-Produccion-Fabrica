# ADA-Produccion-Fabrica

Trabajo de Análisis y Diseño de Algoritmos: **Planificación de Producción en una Fábrica**.
Se combinan **Divide y Vencerás**, **Goloso** y **Programación Dinámica**, y se comparan contra una
asignación **aleatoria**, un **Branch & Bound** exacto (escenario pequeño) y un **Recocido Simulado**.

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
    DivideVenceras.cs           por etapas + división recursiva de máquinas
    ReparacionEnergia.cs        ajuste a E_max (usado por D&V)
    ProgramacionDinamica.cs     dp[i][e] con energía discretizada
    Aleatorio.cs                1000 corridas, promedio y mejor
    BranchAndBound.cs           exacto para n ≤ 8 piezas
    RecocidoSimulado.cs         mover / intercambiar, penalización por E_max
  Experimentos/                 Experimento, Cotas, Comparador (tabla), ExportadorJson, ResumenComparativo
Assets/Scripts/Runner/
  FabricaRunner.cs              MonoBehaviour: ejecuta, Debug.Log de la tabla, exporta JSON (JsonUtility)
Tools/ConsoleTest/              Proyecto .NET 8 (C# 9) que compila Core y corre todo + verificaciones
Resultados/                     escenarioK_{greedy,dc,pd,random,sa[,bb]}.json + resumen_comparativo.json
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
- Ningún algoritmo baja del óptimo ni de la cota.
- Se cumplen las precedencias y no hay solapes en las máquinas.

Termina con código 1 si alguna verificación falla.

## 4. Algoritmos

### Goloso (LPT)

1. Ordena las piezas por trabajo total, de mayor a menor (LPT).
2. Para cada pieza y cada etapa, elige la máquina válida que minimiza `fin + λ·e_ij`, con `λ = β/α` por defecto.
3. Solo acepta una máquina si `energía usada + e_ij + energía mínima de las tareas restantes ≤ E_max`. Con eso el plan siempre es factible.

### Divide y Vencerás

1. **Dividir por etapas:** Corte, Ensamblaje y Empaquetado se resuelven en orden. Los tiempos de liberación de cada etapa son los fines de la etapa anterior.
2. **Dentro de cada etapa** (recursivo):
   - Se divide el grupo de máquinas capaces en dos mitades de capacidad parecida (reparto en serpiente por velocidad).
   - Las tareas (en orden LPT) se reparten entre las dos mitades con el criterio `(carga + w)/Σv + λ·w·ΣP/Σv`.
   - Se recurre en cada mitad. El caso base es una sola máquina.
3. **Combinar:** como las mitades no comparten máquinas, se unen las asignaciones y se secuencia la etapa según los tiempos de liberación. Si al final se excede `E_max`, se reparan movimientos hacia máquinas más eficientes.

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
| Prog. Dinámica    | 260.82 |        9.03 % |
| Divide y Vencerás | 287.04 |       19.99 % |
| Goloso (LPT)      | 309.56 |       29.41 % |

En el escenario grande la diferencia crece:

| Algoritmo    | Makespan | Costo   |
|--------------|---------:|--------:|
| Prog. Dinámica | 806.85 | 1706.37 |
| Goloso (LPT)   | 1538.64 | 2456.27 |

El goloso gasta el presupuesto de energía en las primeras piezas (máquinas rápidas) y obliga a las últimas a ir a máquinas lentas. La PD reparte el presupuesto entre todas las tareas.

### Aleatorio

Cada tarea va a una máquina válida al azar. Se hacen 1000 corridas con semilla fija y se reporta la **mejor** (menor costo entre las factibles) y el **promedio**. El JSON incluye `corridasFactibles`, `makespanPromedio`, `energiaPromedio` y `costoPromedio`.

### Branch & Bound (método extra, exacto)

Búsqueda en profundidad sobre la máquina de cada tarea. La solución del goloso sirve como cota superior inicial. Cada nodo usa estas cotas inferiores:

- `makespan ≥ max(carga de cada máquina, cadena de cada pieza (asignado + tiempo mínimo pendiente), (tiempo total asignado + mínimo restante) / M)`.
- `energía ≥ asignada + mínima restante`.

Se poda si `α·LB_mk + β·LB_e ≥ mejor` o si `LB_e > E_max`. Cada hoja se evalúa con `Simular()`. Solo se usa con 8 piezas o menos, y su resultado es el óptimo de referencia del escenario pequeño, comprobado contra la fuerza bruta.

### Recocido Simulado (método extra)

- **Punto de partida:** la solución del goloso.
- **Vecinos:** mover una tarea a otra máquina válida, o intercambiar las máquinas de dos tareas.
- **Objetivo:** `costo + ρ·max(0, energía − E_max)`, con `ρ = 10·costo₀/E_max`.
- **Enfriamiento:** geométrico de `0.05·costo₀` a `10⁻⁴·costo₀`, en 20 000 iteraciones.
- Devuelve la mejor solución **factible**.

## 5. Complejidades

`n` = tareas (3 × piezas), `m` = máquinas, `P` = piezas, `R` = resolución de energía de la PD, `I` = iteraciones del recocido y `K` = corridas del aleatorio. Una llamada a `Simular()` cuesta `O(n·P)`.

| Algoritmo           | Tiempo                                | Espacio      | Comentario |
|---------------------|---------------------------------------|--------------|------------|
| Simular             | O(n·P)                                | O(n + m)     | En cada paso elige entre ≤ P tareas listas |
| Goloso (LPT)        | O(P log P + n·m)                      | O(n + m)     | Orden LPT + una pasada evaluando m máquinas |
| Divide y Vencerás   | O(n log n + n log m) + reparación     | O(n + m)     | T(n, m) = T(n₁, m/2) + T(n₂, m/2) + O(n): profundidad log m, O(n) por nivel. La reparación, solo si hace falta, es O(k·n·m) |
| Prog. Dinámica      | O(n·R·m²)                             | O(n·R + R·m) | n·R estados × m transiciones × copia del vector de m cargas. Pseudo-polinomial |
| Aleatorio           | O(K·n·P)                              | O(n)         | K simulaciones |
| Branch & Bound      | O(mⁿ·n·P) peor caso                   | O(n + m)     | En la práctica, la poda explora muy pocos nodos (ver `notas` en el JSON) |
| Recocido Simulado   | O(I·n·P)                              | O(n)         | Una simulación por vecino |

## 6. Resultados (semilla 2024)

```
=== Escenario 1: Pequeño (6 piezas, 18 tareas, 3 máquinas) | E_max = 1047.87 | α = 1 β = 0.1 ===
Referencia: óptimo de Branch & Bound, costo = 239.22
Algoritmo              |  Makespan |   Energía |     Costo |  Gap vs ópt (%) | Factible
Goloso (LPT)           |    204.81 |   1047.50 |    309.56 |           29.41 | sí
Divide y Vencerás      |    184.01 |   1030.26 |    287.04 |           19.99 | sí
Prog. Dinámica         |    156.58 |   1042.46 |    260.82 |            9.03 | sí
Aleatorio (mejor)      |    147.65 |   1037.80 |    251.43 |            5.10 | sí
Aleatorio (promedio)   |    160.85 |   1091.39 |    269.99 |           12.86 | NO
Recocido Simulado      |    135.67 |   1046.64 |    240.33 |            0.47 | sí
Branch & Bound         |    134.76 |   1044.56 |    239.22 |            0.00 | sí

=== Escenario 2: Mediano (20 piezas, 60 tareas, 5 máquinas) | E_max = 4470.64 ===
Referencia: COTA INFERIOR (no es el óptimo), costo ≥ 544.61
Goloso (LPT)           |    255.35 |   4470.04 |    702.36 |           28.97 | sí
Divide y Vencerás      |    240.31 |   4466.88 |    687.00 |           26.15 | sí
Prog. Dinámica         |    202.23 |   4436.04 |    645.83 |           18.59 | sí
Aleatorio (mejor)      |    245.29 |   4452.92 |    690.59 |           26.80 | sí
Aleatorio (promedio)   |    251.33 |   4556.12 |    706.94 |           29.81 | NO
Recocido Simulado      |    168.50 |   4470.47 |    615.55 |           13.03 | sí

=== Escenario 3: Grande (50 piezas, 150 tareas, 8 máquinas) | E_max = 9176.58 ===
Referencia: COTA INFERIOR (no es el óptimo), costo ≥ 1037.41
Goloso (LPT)           |   1538.64 |   9176.34 |   2456.27 |          136.77 | sí
Divide y Vencerás      |   1708.64 |   9158.04 |   2624.44 |          152.98 | sí
Prog. Dinámica         |    806.85 |   8995.12 |   1706.37 |           64.48 | sí
Aleatorio (mejor)      |    473.50 |  10300.67 |   1503.57 |           44.93 | NO
Aleatorio (promedio)   |    664.42 |  10130.53 |   1677.47 |           61.70 | NO
Recocido Simulado      |    714.33 |   9173.42 |   1631.68 |           57.28 | sí
```

Notas sobre la tabla:

- En los escenarios 2 y 3, el gap se mide contra una **cota inferior** que no tiene por qué ser alcanzable. Por eso el gap real es menor que el reportado.
- "Aleatorio (promedio)" aparece como **NO** factible porque no todas las corridas cumplen `E_max`.
- En el escenario 3, ninguna de las 1000 corridas aleatorias fue factible.

Los tiempos de ejecución (ms) están en la salida de consola y en `Resultados/resumen_comparativo.json`.
