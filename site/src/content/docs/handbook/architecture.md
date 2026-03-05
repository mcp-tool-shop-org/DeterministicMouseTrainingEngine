---
title: Architecture
description: Four-module modular monolith with enforced boundaries.
sidebar:
  order: 2
---

DMTE is a four-module modular monolith. No cycles, no platform leakage into libraries.

## Dependency graph

```
MouseTrainer.Domain        → (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    → Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         → Domain             Cue system, asset verification
MouseTrainer.MauiHost      → all three          Composition root, MAUI platform host
```

Domain is the leaf. MauiHost is the only composition root. Simulation and Audio depend only on Domain, never on each other.

## Key design principles

### Determinism is constitutional

Same seed produces the same simulation and the same score, always. The engine avoids `DateTime.Now`, `Random`, and platform-dependent floats in the hot path. All randomness comes from a seeded `xorshift32` RNG.

### Protocol-grade identity

`MutatorId`, `ModeId`, and `RunId` are permanent identifiers. Once created, they are frozen forever. FNV-1a 64-bit hashing with canonical parameter serialization ensures the same seed + mode + mutators always produces the same RunId on every platform.

### Warnings are errors

Library projects use `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. The MAUI host opts out only for SDK-generated warnings that are outside the project's control.

## Domain module

The leaf module provides shared primitives:

- **Events** — `GameEvent`, `GameEventType`
- **Input** — `PointerInput` (cursor position)
- **Runs** — `RunDescriptor`, `RunId`, `MutatorId`, `MutatorSpec`, `MutatorParam`, `ModeId`, `DifficultyTier`
- **Utility** — `DeterministicRng` (xorshift32)

## Simulation module

The deterministic simulation engine:

- **Core** — `DeterministicLoop` (60Hz fixed timestep with accumulator), `FrameResult`, `IGameSimulation`
- **Levels** — `LevelBlueprint`, `ILevelGenerator`, `LevelGeneratorRegistry`
- **Modes** — Game mode implementations (e.g., ReflexGates)
- **Mutators** — `IBlueprintMutator`, `MutatorPipeline`, `MutatorRegistry`, six built-in mutators
- **Session** — `SessionController`, `SessionModels`

## Audio module

Audio cue system with asset verification:

- **Assets** — `AssetManifest`, `AssetVerifier`, `IAssetOpener`
- **Core** — `AudioDirector`, `AudioCue`, `AudioCueMap`, `IAudioSink`
