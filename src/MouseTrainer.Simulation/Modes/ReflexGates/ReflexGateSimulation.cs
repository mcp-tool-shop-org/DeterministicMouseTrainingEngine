using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;
using MouseTrainer.Simulation.Core;
using MouseTrainer.Simulation.Debug;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Domain.Utility;

namespace MouseTrainer.Simulation.Modes.ReflexGates;

/// <summary>
/// Reflex Gates — the flagship game mode.
/// Auto-scrolling corridor with oscillating gate apertures.
/// Pure deterministic simulation: no rendering, no platform dependencies.
/// </summary>
public sealed class ReflexGateSimulation : IGameSimulation, ISimDebugOverlay
{
    private readonly ReflexGateConfig _cfg;

    private Gate[] _gates = Array.Empty<Gate>();
    private int _nextGateIndex;
    private float _scrollPosition;
    private int _comboStreak;
    private int _totalScore;
    private bool _levelComplete;
    private long _startTick;

    public ReflexGateSimulation(ReflexGateConfig? cfg = null)
    {
        _cfg = cfg ?? new ReflexGateConfig();
    }

    // --- Read-only accessors for the host/renderer ---
    public IReadOnlyList<Gate> Gates => _gates;
    public int NextGateIndex => _nextGateIndex;
    public float ScrollPosition => _scrollPosition;
    public int ComboStreak => _comboStreak;
    public int TotalScore => _totalScore;
    public bool IsLevelComplete => _levelComplete;

    public void Reset(uint sessionSeed)
    {
        _gates = GenerateGates(sessionSeed);
        _nextGateIndex = 0;
        _scrollPosition = 0f;
        _comboStreak = 0;
        _totalScore = 0;
        _levelComplete = false;
        _startTick = -1;
    }

    /// <summary>
    /// Reset from a pre-generated blueprint. Avoids re-generating gates.
    /// Produces identical simulation state to Reset(uint) when the blueprint
    /// was generated from the same seed + config.
    /// </summary>
    public void Reset(LevelBlueprint blueprint)
    {
        _gates = new Gate[blueprint.Gates.Count];
        for (int i = 0; i < blueprint.Gates.Count; i++)
            _gates[i] = blueprint.Gates[i];

        _nextGateIndex = 0;
        _scrollPosition = 0f;
        _comboStreak = 0;
        _totalScore = 0;
        _levelComplete = false;
        _startTick = -1;
    }

    public void FixedUpdate(long tick, float dt, in PointerInput input, List<GameEvent> events)
    {
        if (_levelComplete) return;

        if (_startTick < 0) _startTick = tick;

        // 1. Advance scroll
        _scrollPosition += _cfg.ScrollSpeed * dt;

        // 2. Deterministic sim time from tick count (avoids float drift)
        float simTime = (tick - _startTick) * dt;

        // 3. Check gate crossings
        while (_nextGateIndex < _gates.Length)
        {
            ref readonly var gate = ref _gates[_nextGateIndex];

            if (_scrollPosition < gate.WallX)
                break;

            float normalizedOffset = gate.NormalizedOffset(input.Y, simTime);

            if (normalizedOffset <= 1.0f)
            {
                // --- PASS ---
                float t = normalizedOffset;
                int score = (int)(_cfg.CenterScore + t * (_cfg.EdgeScore - _cfg.CenterScore));
                if (score < _cfg.EdgeScore) score = _cfg.EdgeScore;
                _totalScore += score;
                _comboStreak++;

                float intensity = 1f - normalizedOffset * 0.5f;

                events.Add(new GameEvent(
                    GameEventType.EnteredGate,
                    Intensity: intensity,
                    Arg0: _nextGateIndex,
                    Arg1: score));

                if (_comboStreak > 0 && _comboStreak % _cfg.ComboThreshold == 0)
                {
                    events.Add(new GameEvent(
                        GameEventType.ComboUp,
                        Intensity: 1f,
                        Arg0: _comboStreak));
                }
            }
            else
            {
                // --- FAIL ---
                _comboStreak = 0;

                float missDistance = normalizedOffset - 1f;
                float intensity = MathF.Min(missDistance, 1f);

                // Arg1 encodes miss distance as int (x1000 for precision)
                int missDistanceEncoded = (int)(MathF.Min(normalizedOffset - 1f, 10f) * 1000f);

                events.Add(new GameEvent(
                    GameEventType.HitWall,
                    Intensity: intensity,
                    Arg0: _nextGateIndex,
                    Arg1: missDistanceEncoded));
            }

            _nextGateIndex++;
        }

        // 4. Level completion
        if (_nextGateIndex >= _gates.Length && !_levelComplete)
        {
            _levelComplete = true;
            events.Add(new GameEvent(
                GameEventType.LevelComplete,
                Intensity: 1f,
                Arg0: _totalScore,
                Arg1: _comboStreak));
        }

        // 5. Heartbeat tick (consistent with stub)
        events.Add(new GameEvent(
            GameEventType.Tick,
            1f,
            Arg0: (int)(tick % int.MaxValue)));
    }

    // --- ISimDebugOverlay ---

    public bool TryGetGatePreview(float simTimeSeconds, out GatePreview preview)
    {
        if (_gates.Length == 0 || _nextGateIndex >= _gates.Length)
        {
            preview = default;
            return false;
        }

        ref readonly var g = ref _gates[_nextGateIndex];

        preview = new GatePreview(
            WallX: g.WallX,
            CenterY: g.CurrentCenterY(simTimeSeconds),
            ApertureHeight: g.ApertureHeight,
            GateIndex: _nextGateIndex,
            ScrollX: _scrollPosition);

        return true;
    }

    private Gate[] GenerateGates(uint sessionSeed)
    {
        var rng = new DeterministicRng(sessionSeed);
        var gates = new Gate[_cfg.GateCount];
        float playfieldCenterY = _cfg.PlayfieldHeight * 0.5f;

        for (int i = 0; i < _cfg.GateCount; i++)
        {
            float t = _cfg.GateCount <= 1 ? 0f : (float)i / (_cfg.GateCount - 1);

            float aperture = _cfg.BaseApertureHeight
                + t * (_cfg.MinApertureHeight - _cfg.BaseApertureHeight);

            float amplitude = _cfg.BaseAmplitude
                + t * (_cfg.MaxAmplitude - _cfg.BaseAmplitude);

            float freq = _cfg.BaseFreqHz
                + t * (_cfg.MaxFreqHz - _cfg.BaseFreqHz);

            float phase = rng.NextFloat01() * 2f * MathF.PI;

            float maxCenterVariation = playfieldCenterY - amplitude - aperture * 0.5f;
            if (maxCenterVariation < 0f) maxCenterVariation = 0f;
            float centerVariation = (rng.NextFloat01() * 2f - 1f) * maxCenterVariation * 0.3f;
            float restCenterY = playfieldCenterY + centerVariation;

            gates[i] = new Gate
            {
                WallX = _cfg.FirstGateX + i * _cfg.GateSpacingX,
                RestCenterY = restCenterY,
                ApertureHeight = aperture,
                Amplitude = amplitude,
                Phase = phase,
                FreqHz = freq,
            };
        }

        return gates;
    }
}
