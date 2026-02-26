<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.md">English</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

Construído com .NET 8 MAUI (com foco no Windows), com uma simulação determinística de passo fixo, modificadores de blueprint compostáveis e uma identidade de execução estável em diferentes plataformas.

---

## Arquitetura

Monolito modular composto por quatro módulos. Sem ciclos, sem vazamento de informações da plataforma para as bibliotecas.

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

Consulte [`docs/modular.manifesto.md`](docs/modular.manifesto.md) para o gráfico de dependências completo e as regras constitucionais.

---

## Modos de Jogo

### ReflexGates

Desafio de portais com rolagem lateral. Aberturas oscilantes em paredes verticais — navegue o cursor por cada portal antes que a rolagem o alcance. Semente determinística → nível idêntico a cada vez.

- Passo fixo de 60 Hz com compensação baseada em acumulador.
- Gerador de números aleatórios `xorshift32` com semente definida para cada execução, garantindo a estabilidade em diferentes plataformas.
- Hashing FNV-1a de 64 bits para a identidade da execução (a mesma semente + modo + modificadores resultam no mesmo RunId em todos os lugares).

---

## Modificadores de Blueprint

Seis transformações compostáveis que remodelam os níveis gerados antes de iniciar o jogo. Aplicadas como uma sequência ordenada sobre o `LevelBlueprint`:

| Modificador | Parâmetros Principais | Efeito |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | Reduz a altura das aberturas — espaços mais estreitos. |
| **WideMargin** | `pct` ∈ [0,1] | Aumenta a altura das aberturas — mais tolerante. |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | Remapeia a dificuldade dos portais por índice — concentra a dificuldade no início ou no final. |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | Quantiza as fases dos portais em N divisões — padrões rítmicos. |
| **GateJitter** | `str` ∈ [0,1] | Deslocamento vertical determinístico via `sin()` — perturbação espacial. |
| **SegmentBias** | `seg`, `amt`, `shape` | Divide os portais em segmentos com viés de dificuldade por segmento. |

Os modificadores são funções puras: `LevelBlueprint → LevelBlueprint`. Eles são combinados via pipeline (`specs.Aggregate`), são resolvidos como fábricas a partir de `MutatorRegistry`, e seus parâmetros são fixados no hash do `RunId` para garantir a reprodutibilidade.

### Formas de Viés de Segmento

- **Crescendo** (shape=0): Começo fácil → final difícil. `d = 2t - 1`
- **Valley** (shape=1): Meio difícil, extremos fáceis. `d = 8t(1-t) - 1`
- **Wave** (shape=2): Segmentos alternados de fácil/difícil. `d = (-1)^k`

---

## Estrutura do Projeto

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

## Construção e Testes

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## Princípios de Design Chave

- **O determinismo é fundamental.** A mesma semente → a mesma simulação → a mesma pontuação, sempre. Sem `DateTime.Now`, sem `Random`, sem números de ponto flutuante dependentes da plataforma no código crítico.
- **Monolito modular, não microserviços.** Quatro assemblies com dependências de mão única. O domínio é a camada mais baixa; o MauiHost é a única raiz de composição.
- **Identidade de nível de protocolo.** `MutatorId`, `ModeId`, `RunId` são permanentes — uma vez criados, são fixos para sempre. Hashing FNV-1a com serialização canônica de parâmetros.
- **Avisos são erros.** Projetos de biblioteca usam `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. O host MAUI opta por não usar essa configuração (devido a avisos gerados pelo SDK).

---

## Licença

[MIT](LICENSE)

> Desenvolvido por [MCP Tool Shop](https://mcp-tool-shop.github.io/)
