---
title: Getting Started
description: Build, test, and run the engine.
sidebar:
  order: 1
---

## Requirements

- .NET 8 SDK
- Windows (MAUI host is Windows-first)
- Visual Studio recommended for the MAUI host project

## Build the simulation library

```bash
dotnet build src/MouseTrainer.Simulation/
```

The library builds with `TreatWarningsAsErrors` enabled — zero warnings allowed.

## Run the tests

```bash
dotnet test tests/MouseTrainer.Tests/
```

All 214 tests cover six categories:

| Category | What it tests |
|----------|--------------|
| Architecture | Dependency boundary enforcement |
| Determinism | Replay regression, RNG, session controller |
| Levels | Generator extraction |
| Mutators | Blueprint mutator correctness + composition |
| Persistence | Session store |
| Runs | RunDescriptor golden hashes + identity |

## Run the MAUI host

The MAUI host is the composition root that wires everything together. Use Visual Studio with the startup project set to `MouseTrainer.MauiHost`.

## Project structure

```
src/
  MouseTrainer.Domain/          Leaf module — events, input, runs, RNG
  MouseTrainer.Simulation/      Deterministic simulation engine
  MouseTrainer.Audio/           Audio cue system
  MouseTrainer.MauiHost/        MAUI composition root (Windows)

tests/
  MouseTrainer.Tests/           214 tests across 6 categories

tools/
  MouseTrainer.AudioGen/        Audio asset generation tooling

docs/
  modular.manifesto.md          Dependency graph + constitutional rules
```
