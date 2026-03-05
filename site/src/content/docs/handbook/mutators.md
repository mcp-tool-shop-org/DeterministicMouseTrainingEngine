---
title: Blueprint Mutators
description: Six composable transforms that reshape generated levels.
sidebar:
  order: 4
---

Blueprint mutators are pure functions that transform a `LevelBlueprint` before play begins. They compose via an ordered pipeline (`specs.Aggregate`), are factory-resolved from `MutatorRegistry`, and their parameters are frozen into the `RunId` hash for reproducibility.

## Available mutators

| Mutator | Key Params | Effect |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | Scales aperture heights down — tighter gaps |
| **WideMargin** | `pct` ∈ [0,1] | Scales aperture heights up — more forgiving |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | Remaps gate difficulty by index — front-load or back-load |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | Quantizes gate phases to N divisions — rhythmic patterns |
| **GateJitter** | `str` ∈ [0,1] | Deterministic vertical offset via sin() — spatial perturbation |
| **SegmentBias** | `seg`, `amt`, `shape` | Divides gates into acts with per-segment difficulty bias |

## Composition

Mutators are applied as an ordered fold: `LevelBlueprint → LevelBlueprint`. The pipeline processes each mutator spec in sequence, passing the output of one as the input to the next.

Since mutator parameters are frozen into the RunId hash, any change to the mutator pipeline produces a different RunId — ensuring replay integrity.

## SegmentBias shapes

The SegmentBias mutator supports three difficulty distribution shapes:

| Shape | Name | Formula | Behavior |
|-------|------|---------|----------|
| 0 | **Crescendo** | `d = 2t - 1` | Easy start → hard finish |
| 1 | **Valley** | `d = 8t(1-t) - 1` | Hard middle, easy ends |
| 2 | **Wave** | `d = (-1)^k` | Alternating easy/hard segments |

Each shape distributes difficulty differently across the level's segments, creating distinct play experiences from the same seed.

## Identity integration

Every mutator has a `MutatorId` (stable string identifier) and `MutatorSpec` (id + parameters). The `MutatorParam` values are serialized canonically into the RunId hash using FNV-1a 64-bit hashing. This means:

- Same mutator pipeline + same seed = same RunId on every platform
- Changing any parameter produces a new RunId
- Mutator order matters — different ordering produces different levels and different RunIds
