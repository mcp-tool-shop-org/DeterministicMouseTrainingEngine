---
title: Game Modes
description: ReflexGates and the pluggable simulation interface.
sidebar:
  order: 3
---

## ReflexGates

The primary game mode. A side-scrolling gate challenge where oscillating apertures appear on vertical walls — navigate the cursor through each gate before the scroll catches you.

Key properties:

- Fixed 60Hz timestep with accumulator-based catch-up
- `xorshift32` RNG seeded per run for platform-stable generation
- FNV-1a 64-bit hashing for run identity — same seed + mode + mutators = same RunId everywhere
- Deterministic seed produces identical levels every time

## The simulation interface

`IGameSimulation` is a two-method contract. New game modes plug in without touching the engine core:

1. **Update** — advance the simulation by one fixed timestep
2. **Render** — produce a frame result with alpha interpolation

The `DeterministicLoop` drives the simulation at 60Hz using an accumulator pattern. When the frame budget allows, it calls Update one or more times to catch up, then calls Render once with an alpha value for smooth interpolation between simulation states.

## Level generation

Levels are generated through a pipeline:

1. `ILevelGenerator` produces a `LevelBlueprint` from a seed
2. The `MutatorPipeline` transforms the blueprint through an ordered fold of `IBlueprintMutator` instances
3. The final blueprint is used by the game mode simulation

The `LevelGeneratorRegistry` resolves generators by game mode, and the `MutatorRegistry` resolves mutators by `MutatorId`. Both are factory-based for extensibility.
