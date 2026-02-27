# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-02-27

### Added
- Shipcheck compliance: SECURITY.md, CHANGELOG.md, SHIP_GATE.md, SCORECARD.md
- Security & Data Scope section in README

### Changed
- Promoted from v0.1.0 to v1.0.0

## [0.1.0]

### Added
- Deterministic 60Hz fixed-timestep simulation engine
- ReflexGates game mode with oscillating apertures
- Six composable blueprint mutators (NarrowMargin, WideMargin, DifficultyCurve, RhythmLock, GateJitter, SegmentBias)
- xorshift32 RNG for platform-stable level generation
- FNV-1a 64-bit run identity hashing
- Event-driven audio cue system with asset verification
- Four-module modular monolith architecture (Domain, Simulation, Audio, MauiHost)
- 214 tests across architecture, determinism, levels, mutators, persistence, and runs
