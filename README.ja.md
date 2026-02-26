<p align="center">
  <a href="README.md">English</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

.NET 8 MAUI (Windowsを優先)上に構築され、完全に決定論的な固定タイムステップシミュレーション、組み合わせ可能なブループリントミューテータ、およびプラットフォームに依存しない実行IDを備えています。

---

## アーキテクチャ

4モジュールのモジュール型モノリス。サイクルがなく、ライブラリへのプラットフォーム依存もありません。

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

詳細な依存関係グラフと設計原則については、[`docs/modular.manifesto.md`](docs/modular.manifesto.md) を参照してください。

---

## ゲームモード

### ReflexGates

横スクロール型のゲートチャレンジ。垂直壁に配置された振動する開口部を通過し、スクロールが追いつく前に各ゲートを通過します。決定論的なシードを使用するため、毎回同じレベルになります。

- 固定の60Hzタイムステップで、アキュムレータベースの補正を行います。
- プラットフォームに依存しない生成のために、`xorshift32` RNGを各実行ごとにシードします。
- 実行IDには、FNV-1a 64ビットハッシュを使用します（同じシード + モード + ミューテータ = どこでも同じRunId）。

---

## ブループリントミューテータ

生成されたレベルをゲーム開始前に変更する、6つの組み合わせ可能な変換です。`LevelBlueprint`に対して、順序付けられたfoldとして適用されます。

| ミューテータ | 主要なパラメータ | 効果 |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | 開口部の高さを縮小し、より狭い隙間を作ります。 |
| **WideMargin** | `pct` ∈ [0,1] | 開口部の高さを大きくし、より寛容な設定にします。 |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | インデックスごとにゲートの難易度を再マッピングし、前置きまたは後置きを行います。 |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | ゲートのフェーズをN分割に量子化し、リズミカルなパターンを作成します。 |
| **GateJitter** | `str` ∈ [0,1] | sin()関数による決定論的な垂直オフセット — 空間的な摂動。 |
| **SegmentBias** | `seg`, `amt`, `shape` | ゲートを複数のセグメントに分割し、各セグメントに難易度のバイアスを適用します。 |

ミューテータは純粋な関数です：`LevelBlueprint → LevelBlueprint`。`pipeline`（`specs.Aggregate`）によって結合され、`MutatorRegistry`からファクトリによって解決され、パラメータは再現性を確保するために`RunId`ハッシュに固定されます。

### セグメントバイアス形状

- **Crescendo** (shape=0): 易しい開始 → 難しい終了。 `d = 2t - 1`
- **Valley** (shape=1): 難しい中間、易しい終了。 `d = 8t(1-t) - 1`
- **Wave** (shape=2): 易しい/難しいセグメントが交互に繰り返される。 `d = (-1)^k`

---

## プロジェクト構造

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

## ビルドとテスト

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## 主要な設計原則

- **決定論は原則です。** 同じシード → 同じシミュレーション → 同じスコア、常に。`DateTime.Now`、`Random`、ホットパスにおけるプラットフォーム依存の浮動小数点数を使用しません。
- **モジュール型モノリス、マイクロサービスではありません。** 一方向の依存関係が強制された4つのアセンブリ。ドメインが葉であり、MauiHostのみがコンポジションのルートです。
- **プロトコルレベルのID。** `MutatorId`、`ModeId`、`RunId`は永続的であり、作成されると永久に固定されます。FNV-1aハッシュと、標準的なパラメータシリアル化を使用します。
- **警告はエラーです。** ライブラリプロジェクトでは、`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`を使用します。MAUIホストは、SDKによって生成される警告を除き、この設定を無効にします。

---

## ライセンス

[MIT](LICENSE)

> 作成者: [MCP Tool Shop](https://mcp-tool-shop.github.io/)
