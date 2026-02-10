# MouseTrainer Modular Architecture Manifesto

> Constitutional rules for the MouseTrainer module graph.
> Treat violations as build failures.

---

## Module Inventory

| Module | Assembly | Purpose |
|--------|----------|---------|
| **Domain** | `MouseTrainer.Domain` | Shared primitives: events, input, utility |
| **Simulation** | `MouseTrainer.Simulation` | Deterministic game loop, modes, debug overlay |
| **Audio** | `MouseTrainer.Audio` | Cue system, asset manifest, verification |
| **App (MAUI)** | `MouseTrainer.MauiHost` | Platform host — wires everything together |

---

## Dependency Graph (Allowed References Only)

```
MouseTrainer.Domain        --> (nothing)
MouseTrainer.Simulation    --> MouseTrainer.Domain
MouseTrainer.Audio         --> MouseTrainer.Domain
MouseTrainer.MauiHost      --> Domain + Simulation + Audio
```

### Prohibited References

- `Audio` must **never** reference `Simulation`
- `Simulation` must **never** reference `Audio`
- `Domain` must **never** reference any sibling module
- No library module may reference `Microsoft.Maui.*` or any platform SDK
- No "mode" (`Simulation.Modes.*`) may reference another mode

---

## Namespace Conventions

Flat namespaces — no stutter (e.g., no `MouseTrainer.Audio.Audio`).

| Folder Path | Namespace |
|-------------|-----------|
| `Domain/Events/` | `MouseTrainer.Domain.Events` |
| `Domain/Input/` | `MouseTrainer.Domain.Input` |
| `Domain/Utility/` | `MouseTrainer.Domain.Utility` |
| `Simulation/Core/` | `MouseTrainer.Simulation.Core` |
| `Simulation/Modes/ReflexGates/` | `MouseTrainer.Simulation.Modes.ReflexGates` |
| `Simulation/Debug/` | `MouseTrainer.Simulation.Debug` |
| `Audio/Core/` | `MouseTrainer.Audio.Core` |
| `Audio/Assets/` | `MouseTrainer.Audio.Assets` |
| `MauiHost/` | `MouseTrainer.MauiHost` |

### Why `DeterministicRng` Lives in Domain

`AudioDirector.EmitOneShot` calls `DeterministicRng.Mix()` for deterministic
cue selection. Placing RNG in Simulation would force `Audio --> Simulation`,
violating the one-way graph. Domain is the correct home.

---

## Compiler Guardrails

Defined in `src/Directory.Build.props`:

- `<Nullable>enable</Nullable>` — all projects
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — library projects only
- `<AnalysisLevel>latest-recommended</AnalysisLevel>` — all projects
- MAUI host opts out of warnings-as-errors (SDK-generated warnings)

---

## Enforcement Roadmap

### Current (Phase 2A)

- [x] Module split with enforced project references
- [x] `Directory.Build.props` with nullable + warnings-as-errors
- [x] This manifesto (constitutional documentation)

### Next (Phase 2C — ArchTests)

- [ ] `MouseTrainer.ArchTests` project using NetArchTest
- [ ] Rules codified as unit tests:
  - `Simulation.*` must not reference `Microsoft.Maui.*`
  - `Audio.*` must not reference `MouseTrainer.Simulation.*`
  - `Domain.*` must not reference any sibling
  - `Simulation.Modes.*` must not cross-reference other modes
- [ ] Run in CI — violation = red build

---

## Adding a New Game Mode

1. Create folder: `Simulation/Modes/NewMode/`
2. Namespace: `MouseTrainer.Simulation.Modes.NewMode`
3. Implement `IGameSimulation` (and optionally `ISimDebugOverlay`)
4. Only reference `MouseTrainer.Simulation.Core` and `MouseTrainer.Domain.*`
5. Wire in `MauiHost` — the host is the only composition root
6. Never reference other modes directly

---

## Adding a New Module

Before creating any new assembly:

1. Draw the updated dependency graph
2. Verify no cycles are introduced
3. Add the module to this manifesto
4. Add arch test rules for the new module
5. Get team sign-off
