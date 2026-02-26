<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.md">English</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

该项目基于 .NET 8 MAUI（Windows 优先），采用完全确定的固定时间步长模拟，具有可组合的蓝图修改器，以及平台稳定的运行标识。

---

## 架构

四模块的单体结构。没有循环依赖，也没有平台信息泄露到库中。

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

请参阅 [`docs/modular.manifesto.md`](docs/modular.manifesto.md) 以获取完整的依赖关系图和宪法规则。

---

## 游戏模式

### ReflexGates

横向卷轴关卡挑战。垂直墙壁上的振荡开口——在滚动赶上你之前，引导光标穿过每个关卡。确定的种子值 → 每次都生成相同的关卡。

- 固定 60Hz 的时间步长，并使用累加器进行补偿。
- 使用 `xorshift32` 伪随机数生成器，每个运行使用不同的种子，以实现平台稳定的生成。
- 使用 FNV-1a 64 位哈希算法，用于运行标识（相同的种子 + 模式 + 修改器 = 任何地方都相同的 RunId）。

---

## 蓝图修改器

六个可组合的变换，用于在游戏开始前重塑生成的关卡。这些变换按顺序应用于 `LevelBlueprint`。

| 修改器 | 关键参数 | 效果 |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | 缩小开口高度——更小的间隙。 |
| **WideMargin** | `pct` ∈ [0,1] | 增大开口高度——更宽松。 |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | 通过索引重新映射关卡难度——前端加重或后端加重。 |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | 将关卡阶段量化为 N 个分段——产生有节奏的模式。 |
| **GateJitter** | `str` ∈ [0,1] | 通过 sin() 函数实现的确定性垂直偏移——空间扰动。 |
| **SegmentBias** | `seg`, `amt`, `shape` | 将关卡划分为具有每个分段难度偏差的片段。 |

修改器是纯函数：`LevelBlueprint → LevelBlueprint`。 它们通过流水线 (`specs.Aggregate`) 进行组合，从 `MutatorRegistry` 中进行工厂解析，并且它们的参数被冻结到 `RunId` 哈希值中，以实现可重复性。

### 分段偏差形状

- **Crescendo** (shape=0): 从简单开始，逐渐变得困难。 `d = 2t - 1`
- **Valley** (shape=1): 困难的中间部分，简单的开头和结尾。 `d = 8t(1-t) - 1`
- **Wave** (shape=2): 交替的简单/困难片段。 `d = (-1)^k`

---

## 项目结构

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

## 构建与测试

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## 关键设计原则

- **确定性是基本原则。** 相同的种子值 → 相同的模拟 → 相同的得分，始终如一。 不使用 `DateTime.Now`，也不使用 `Random`，以及在关键路径中不使用平台相关的浮点数。
- **单体结构，而不是微服务。** 四个程序集，强制执行单向依赖关系。 核心模块是叶子节点；`MauiHost` 是唯一的组合根。
- **协议级别的标识。** `MutatorId`、`ModeId` 和 `RunId` 是永久的——一旦创建，就永远冻结。 使用 FNV-1a 哈希算法，并使用规范的参数序列化。
- **警告视为错误。** 库项目使用 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。 `MAUI` 主程序选择退出（SDK 生成的警告）。

---

## 许可证

[MIT](LICENSE)

> 由 [MCP Tool Shop](https://mcp-tool-shop.github.io/) 构建。
