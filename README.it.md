<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.md">English</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

Costruito su .NET 8 MAUI (con priorità per Windows), con una simulazione deterministica a intervallo fisso, modificatori di blueprint componibili e un'identità di esecuzione stabile su diverse piattaforme.

---

## Architettura

Monolite modulare composto da quattro moduli. Nessun ciclo, nessuna dipendenza tra le librerie e la piattaforma.

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

Consultare il file [`docs/modular.manifesto.md`](docs/modular.manifesto.md) per il grafico completo delle dipendenze e le regole costituzionali.

---

## Modalità di Gioco

### ReflexGates

Sfida a scorrimento laterale. Aperture oscillanti su pareti verticali: guidare il cursore attraverso ogni porta prima che lo scorrimento vi raggiunga. Seed deterministico → livello identico ogni volta.

- Intervallo fisso di 60 Hz con compensazione basata su accumulatore.
- Generatore di numeri casuali `xorshift32` inizializzato per ogni esecuzione, per una generazione stabile su diverse piattaforme.
- Funzione di hash FNV-1a a 64 bit per l'identificazione dell'esecuzione (lo stesso seed + modalità + modificatori = lo stesso RunId ovunque).

---

## Modificatori di Blueprint

Sei trasformazioni componibili che modificano i livelli generati prima dell'esecuzione. Applicate come una sequenza ordinata su `LevelBlueprint`:

| Modificatore | Parametri Chiave | Effetto |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | Riduce l'altezza delle aperture: spazi più stretti. |
| **WideMargin** | `pct` ∈ [0,1] | Aumenta l'altezza delle aperture: spazi più larghi. |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | Rimappa la difficoltà delle porte in base all'indice: difficoltà crescente o decrescente. |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | Quantizza le fasi delle porte in N divisioni: schemi ritmici. |
| **GateJitter** | `str` ∈ [0,1] | Spostamento verticale deterministico tramite la funzione seno: perturbazione spaziale. |
| **SegmentBias** | `seg`, `amt`, `shape` | Divide le porte in atti con una tendenza di difficoltà specifica per ogni segmento. |

I modificatori sono funzioni pure: `LevelBlueprint → LevelBlueprint`. Si combinano tramite pipeline (`specs.Aggregate`), vengono risolti come factory da `MutatorRegistry` e i loro parametri vengono inclusi nell'hash `RunId` per garantire la riproducibilità.

### Forme di SegmentBias

- **Crescendo** (shape=0): Inizio facile → finale difficile. `d = 2t - 1`
- **Valley** (shape=1): Difficoltà elevata al centro, facile all'inizio e alla fine. `d = 8t(1-t) - 1`
- **Wave** (shape=2): Segmenti alternati facili/difficili. `d = (-1)^k`

---

## Struttura del Progetto

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

## Build & Test

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## Principi di Progettazione Chiave

- **Il determinismo è fondamentale.** Lo stesso seed → la stessa simulazione → lo stesso punteggio, sempre. Nessun `DateTime.Now`, nessun `Random`, nessun valore in virgola mobile dipendente dalla piattaforma nel codice critico.
- **Monolite modulare, non microservizi.** Quattro assembly con dipendenze unidirezionali imposte. Il dominio è il componente foglia; `MauiHost` è l'unico punto di composizione.
- **Identità di livello protocollo.** `MutatorId`, `ModeId`, `RunId` sono permanenti: una volta creati, sono congelati per sempre. Funzione di hash FNV-1a con serializzazione canonica dei parametri.
- **Gli avvisi sono errori.** I progetti di libreria utilizzano `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. L'host MAUI esclude questa impostazione (avvisi generati dall'SDK).

---

## Licenza

[MIT](LICENSE)

> Creato da [MCP Tool Shop](https://mcp-tool-shop.github.io/)
