<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.md">English</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

Construit sur .NET 8 MAUI (conçu principalement pour Windows), avec une simulation déterministe à pas fixe, des modificateurs de blueprint composables et une identité de session stable, indépendante de la plateforme.

---

## Architecture

Architecture monolithique modulaire en quatre modules. Pas de cycles, pas de fuite de la plateforme vers les bibliothèques.

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

Consultez [`docs/modular.manifesto.md`](docs/modular.manifesto.md) pour le graphe de dépendances complet et les règles constitutionnelles.

---

## Modes de jeu

### ReflexGates

Un défi de portes en défilement latéral. Des ouvertures oscillantes sur des murs verticaux : guide le curseur à travers chaque porte avant que le défilement ne vous rattrape. Une graine déterministe garantit le même niveau à chaque fois.

- Pas fixe de 60 Hz avec correction basée sur un accumulateur.
- Générateur de nombres aléatoires `xorshift32` initialisé pour chaque session, garantissant une génération stable, indépendante de la plateforme.
- Hachage FNV-1a 64 bits pour l'identité de la session (la même graine + mode + modificateurs = le même RunId partout).

---

## Modificateurs de blueprint

Six transformations composables qui modifient les niveaux générés avant le jeu. Elles sont appliquées dans un ordre précis sur le `LevelBlueprint` :

| Modificateur | Paramètres clés | Effet |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | Réduit la hauteur des ouvertures, créant des espaces plus étroits. |
| **WideMargin** | `pct` ∈ [0,1] | Augmente la hauteur des ouvertures, rendant le jeu plus facile. |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | Remappage de la difficulté des portes en fonction de leur index, créant une progression de difficulté (début facile ou difficile). |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | Quantifie les phases des portes en N divisions, créant des motifs rythmiques. |
| **GateJitter** | `str` ∈ [0,1] | Décalage vertical déterministe via la fonction sin(), créant une perturbation spatiale. |
| **SegmentBias** | `seg`, `amt`, `shape` | Divise les portes en segments avec un biais de difficulté par segment. |

Les modificateurs sont des fonctions pures : `LevelBlueprint → LevelBlueprint`. Ils sont composés via un pipeline (`specs.Aggregate`), sont résolus à partir du `MutatorRegistry`, et leurs paramètres sont figés dans le hachage `RunId` pour garantir la reproductibilité.

### Formes de biais de segment

- **Crescendo** (shape=0) : Début facile → fin difficile. `d = 2t - 1`
- **Valley** (shape=1) : Milieu difficile, début et fin faciles. `d = 8t(1-t) - 1`
- **Wave** (shape=2) : Segments alternant facile/difficile. `d = (-1)^k`

---

## Structure du projet

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

## Construction et tests

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## Principes de conception clés

- **Le déterminisme est constitutionnel.** La même graine → la même simulation → le même score, toujours. Pas de `DateTime.Now`, pas de `Random`, pas de nombres à virgule flottante dépendant de la plateforme dans la partie critique.
- **Monolithe modulaire, pas microservices.** Quatre assemblages avec des dépendances unidirectionnelles imposées. Le domaine est la partie la plus basse ; MauiHost est la seule racine de composition.
- **Identité de qualité protocolaire.** `MutatorId`, `ModeId`, `RunId` sont permanents : une fois créés, ils sont figés pour toujours. Hachage FNV-1a avec une sérialisation canonique des paramètres.
- **Les avertissements sont des erreurs.** Les projets de bibliothèque utilisent `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. L'hôte MAUI est exclu (avertissements générés par le SDK).

---

## Licence

[MIT](LICENSE)

> Développé par [MCP Tool Shop](https://mcp-tool-shop.github.io/)
