<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.md">English</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

Construido con .NET 8 MAUI (con enfoque en Windows), con una simulación determinista de paso de tiempo fijo, modificadores de diseño composables y una identidad de ejecución estable en diferentes plataformas.

---

## Arquitectura

Monolito modular de cuatro módulos. Sin ciclos, sin fugas de la plataforma en las bibliotecas.

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

Consulte [`docs/modular.manifesto.md`](docs/modular.manifesto.md) para ver el gráfico de dependencias completo y las reglas constitucionales.

---

## Modos de juego

### ReflexGates

Desafío de puertas con desplazamiento lateral. Aberturas oscilantes en paredes verticales: guíe el cursor a través de cada puerta antes de que el desplazamiento lo alcance. Semilla determinista → nivel idéntico cada vez.

- Paso de tiempo fijo de 60 Hz con corrección basada en acumulador.
- Generador de números aleatorios `xorshift32` con semilla por ejecución para una generación estable en diferentes plataformas.
- Función de hash FNV-1a de 64 bits para la identidad de la ejecución (la misma semilla + modo + modificadores = el mismo RunId en todas partes).

---

## Modificadores de diseño

Seis transformaciones composables que modifican los niveles generados antes de la ejecución. Se aplican como una operación ordenada sobre `LevelBlueprint`:

| Modificador | Parámetros clave | Efecto |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | Reduce la altura de las aberturas: espacios más estrechos. |
| **WideMargin** | `pct` ∈ [0,1] | Aumenta la altura de las aberturas: más indulgente. |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | Remapea la dificultad de las puertas por índice: dificultad creciente al principio o al final. |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | Cuantifica las fases de las puertas en N divisiones: patrones rítmicos. |
| **GateJitter** | `str` ∈ [0,1] | Desplazamiento vertical determinista mediante sin(): perturbación espacial. |
| **SegmentBias** | `seg`, `amt`, `shape` | Divide las puertas en segmentos con un sesgo de dificultad por segmento. |

Los modificadores son funciones puras: `LevelBlueprint → LevelBlueprint`. Se combinan mediante tuberías (`specs.Aggregate`), se resuelven desde `MutatorRegistry` y sus parámetros se congelan en el hash `RunId` para la reproducibilidad.

### Formas de sesgo de segmento

- **Crescendo** (shape=0): Fácil al principio, difícil al final. `d = 2t - 1`
- **Valley** (shape=1): Difícil en el medio, fácil al principio y al final. `d = 8t(1-t) - 1`
- **Wave** (shape=2): Segmentos fáciles/difíciles alternantes. `d = (-1)^k`

---

## Estructura del proyecto

```
src/
  MouseTrainer.Domain/          Leaf module — events, input, runs, RNG
    Events/                     GameEvent, GameEventType
    Input/                      PointerInput
    Runs/                       RunDescriptor, RunId, MutatorId/Spec/Param, ModeId, DifficultyTier
    Utility/                    DeterministicRng (xorshift32)

  MouseTrainer.Simulation/      Deterministic simulation engine
    Core/                       DeterministicLoop, FrameResult, IGameSimulation
    Debug/                      ISimDebugOverlay
    Levels/                     LevelBlueprint, ILevelGenerator, LevelGeneratorRegistry
    Modes/ReflexGates/          Gate, ReflexGateSimulation, ReflexGateGenerator, ReflexGateConfig
    Mutators/                   IBlueprintMutator, MutatorPipeline, MutatorRegistry, 6 mutators
    Session/                    SessionController, SessionModels

  MouseTrainer.Audio/           Audio cue system
    Assets/                     AssetManifest, AssetVerifier, IAssetOpener
    Core/                       AudioDirector, AudioCue, AudioCueMap, IAudioSink

  MouseTrainer.MauiHost/        MAUI composition root (Windows)

tests/
  MouseTrainer.Tests/           214 tests across 6 categories
    Architecture/               Dependency boundary enforcement
    Determinism/                Replay regression, RNG, session controller
    Levels/                     Generator extraction
    Mutators/                   Blueprint mutator correctness + composition
    Persistence/                Session store
    Runs/                       RunDescriptor golden hashes + identity

tools/
  MouseTrainer.AudioGen/        Audio asset generation tooling

docs/
  modular.manifesto.md          Dependency graph + constitutional rules
  MAUI_AssetOpener_Snippet.md   Platform asset wiring snippet
```

---

## Compilación y pruebas

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## Principios de diseño clave

- **El determinismo es fundamental.** La misma semilla → la misma simulación → la misma puntuación, siempre. No se utiliza `DateTime.Now`, ni `Random`, ni números de punto flotante dependientes de la plataforma en la parte crítica.
- **Monolito modular, no microservicios.** Cuatro ensamblados con dependencias unidireccionales obligatorias. El dominio es la capa más externa; MauiHost es la única raíz de composición.
- **Identidad de grado de protocolo.** `MutatorId`, `ModeId`, `RunId` son permanentes: una vez creados, se congelan para siempre. Función de hash FNV-1a con serialización canónica de parámetros.
- **Las advertencias son errores.** Los proyectos de biblioteca utilizan `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. El host MAUI se excluye (advertencias generadas por el SDK).

---

## Licencia

[MIT](LICENSE)

> Creado por [MCP Tool Shop](https://mcp-tool-shop.github.io/)
