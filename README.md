<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

Built on .NET 8 MAUI (Windows-first), with a fully deterministic fixed-timestep simulation, composable blueprint mutators, and platform-stable run identity.

---

## Architecture

Four-module modular monolith. No cycles, no platform leakage into libraries.

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

See [`docs/modular.manifesto.md`](docs/modular.manifesto.md) for the full dependency graph and constitutional rules.

---

## Game Modes

### ReflexGates

Side-scrolling gate challenge. Oscillating apertures on vertical walls — navigate the cursor through each gate before the scroll catches you. Deterministic seed → identical level every time.

- Fixed 60 Hz timestep with accumulator-based catch-up
- `xorshift32` RNG seeded per run for platform-stable generation
- FNV-1a 64-bit hashing for run identity (same seed + mode + mutators = same RunId everywhere)

---

## Blueprint Mutators

Six composable transforms that reshape generated levels before play. Applied as an ordered fold over `LevelBlueprint`:

| Mutator | Key Params | Effect |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | Scales aperture heights down — tighter gaps |
| **WideMargin** | `pct` ∈ [0,1] | Scales aperture heights up — more forgiving |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | Remaps gate difficulty by index — front-load or back-load |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | Quantizes gate phases to N divisions — rhythmic patterns |
| **GateJitter** | `str` ∈ [0,1] | Deterministic vertical offset via sin() — spatial perturbation |
| **SegmentBias** | `seg`, `amt`, `shape` | Divides gates into acts with per-segment difficulty bias |

Mutators are pure functions: `LevelBlueprint → LevelBlueprint`. They compose via pipeline (`specs.Aggregate`), are factory-resolved from `MutatorRegistry`, and their parameters are frozen into the `RunId` hash for reproducibility.

### SegmentBias Shapes

- **Crescendo** (shape=0): Easy start → hard finish. `d = 2t - 1`
- **Valley** (shape=1): Hard middle, easy ends. `d = 8t(1-t) - 1`
- **Wave** (shape=2): Alternating easy/hard segments. `d = (-1)^k`

---

## Project Structure

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

## Key Design Principles

- **Determinism is constitutional.** Same seed → same simulation → same score, always. No `DateTime.Now`, no `Random`, no platform-dependent floats in the hot path.
- **Modular monolith, not microservices.** Four assemblies with enforced one-way dependencies. Domain is the leaf; MauiHost is the only composition root.
- **Protocol-grade identity.** `MutatorId`, `ModeId`, `RunId` are permanent — once created, frozen forever. FNV-1a hashing with canonical parameter serialization.
- **Warnings are errors.** Library projects use `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. MAUI host opts out (SDK-generated warnings).

---

## Security & Data Scope

Deterministic Mouse Training Engine is a **local-first** .NET MAUI desktop game.

- **Data accessed:** Mouse cursor coordinates (real-time during gameplay), MAUI local storage for session results and run history
- **Data NOT accessed:** No cloud sync. No telemetry. No analytics. No network calls. No authentication
- **Permissions:** Standard .NET MAUI sandbox. File system for local data only. No elevated permissions

Full policy: [SECURITY.md](SECURITY.md)

---

## Scorecard

| Category | Score |
|----------|-------|
| A. Security | 10/10 |
| B. Error Handling | 10/10 |
| C. Operator Docs | 10/10 |
| D. Shipping Hygiene | 10/10 |
| E. Identity (soft) | 10/10 |
| **Overall** | **50/50** |

---

## License

[MIT](LICENSE)

> Built by [MCP Tool Shop](https://mcp-tool-shop.github.io/)
