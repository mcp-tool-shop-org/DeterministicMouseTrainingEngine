import type { SiteConfig } from '@mcptoolshop/site-theme';

export const config: SiteConfig = {
  title: 'Deterministic Mouse Training Engine',
  description: 'Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.',
  logoBadge: 'DM',
  brandName: 'DMTE',
  repoUrl: 'https://github.com/mcp-tool-shop-org/DeterministicMouseTrainingEngine',
  footerText: 'MIT Licensed — built by <a href="https://mcp-tool-shop.github.io/" style="color:var(--color-muted);text-decoration:underline">MCP Tool Shop</a>',

  hero: {
    badge: '60Hz · deterministic · pluggable',
    headline: 'Deterministic Mouse',
    headlineAccent: 'Training Engine',
    description: 'A fully deterministic simulation engine for mouse dexterity training. Fixed timestep, composable blueprint mutators, platform-stable run identity, and replay verification — built on .NET 8 MAUI.',
    primaryCta: { href: '#architecture', label: 'Explore' },
    secondaryCta: { href: 'https://github.com/mcp-tool-shop-org/DeterministicMouseTrainingEngine', label: 'Source' },
    previews: [
      { label: 'Determinism', code: 'Same seed → same simulation → same score' },
      { label: 'Identity', code: 'FNV-1a(seed + mode + mutators) = RunId' },
      { label: 'Timestep', code: '60Hz fixed + alpha interpolation' },
    ],
  },

  sections: [
    {
      kind: 'features',
      id: 'principles',
      title: 'Design Principles',
      subtitle: 'The rules that shape every frame.',
      features: [
        { title: 'Determinism is Constitutional', desc: 'Same seed produces the same simulation and the same score, always. No DateTime.Now, no Random, no platform-dependent floats.' },
        { title: 'Protocol-Grade Identity', desc: 'MutatorId, ModeId, and RunId are permanent. FNV-1a hashing with canonical parameter serialization freezes run identity forever.' },
        { title: 'Warnings are Errors', desc: 'Library projects enforce TreatWarningsAsErrors. MAUI host opts out only for SDK-generated warnings.' },
      ],
    },
    {
      kind: 'code-cards',
      id: 'architecture',
      title: 'Architecture',
      cards: [
        { title: 'Modular Monolith', code: `Domain        → (nothing)     # Leaf: primitives, RNG, run identity
Simulation    → Domain         # Engine: loop, modes, mutators, levels
Audio         → Domain         # Cue system, asset verification
MauiHost      → all three      # Composition root, MAUI platform host` },
        { title: 'Engine Core', code: `IGameSimulation    # Pluggable game mode (2 methods)
DeterministicLoop  # 60Hz fixed timestep + accumulator
DeterministicRng   # xorshift32, seed-reproducible
MutatorPipeline    # Ordered fold over LevelBlueprint` },
      ],
    },
    {
      kind: 'data-table',
      id: 'mutators',
      title: 'Blueprint Mutators',
      subtitle: 'Six composable transforms that reshape generated levels before play.',
      columns: ['Mutator', 'Key Params', 'Effect'],
      rows: [
        ['NarrowMargin', 'pct ∈ [0,1]', 'Scales aperture heights down — tighter gaps'],
        ['WideMargin', 'pct ∈ [0,1]', 'Scales aperture heights up — more forgiving'],
        ['DifficultyCurve', 'exp ∈ [0.1,5]', 'Remaps gate difficulty by index'],
        ['RhythmLock', 'div ∈ {2,3,4,6,8}', 'Quantizes gate phases — rhythmic patterns'],
        ['GateJitter', 'str ∈ [0,1]', 'Deterministic vertical offset via sin()'],
        ['SegmentBias', 'seg, amt, shape', 'Per-segment difficulty bias (Crescendo/Valley/Wave)'],
      ],
    },
    {
      kind: 'features',
      id: 'game-modes',
      title: 'Game Modes',
      features: [
        { title: 'ReflexGates', desc: 'Side-scrolling gate challenge. Oscillating apertures on vertical walls — navigate the cursor through each gate before the scroll catches you.' },
        { title: 'Pluggable Interface', desc: 'IGameSimulation is a two-method contract. New game modes plug in without touching the engine core.' },
        { title: 'Level Generation', desc: 'LevelBlueprint + ILevelGenerator + MutatorPipeline. Deterministic seed produces identical levels across platforms.' },
      ],
    },
    {
      kind: 'code-cards',
      id: 'build',
      title: 'Build & Test',
      cards: [
        { title: 'Build', code: `# Build simulation library (0 warnings)
dotnet build src/MouseTrainer.Simulation/

# Run MAUI host (Windows)
# Use Visual Studio, set startup to MauiHost` },
        { title: 'Test', code: `# Run all 214 tests across 6 categories
dotnet test tests/MouseTrainer.Tests/

# Architecture, Determinism, Levels,
# Mutators, Persistence, Runs` },
      ],
    },
  ],
};
