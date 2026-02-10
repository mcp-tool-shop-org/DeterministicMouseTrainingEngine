using System.Diagnostics;
using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Runs;

namespace MouseTrainer.Simulation.Session;

/// <summary>
/// State machine for a single game session: Ready → Playing → Results.
/// Processes GameEvents emitted by the simulation to build per-gate results.
/// No MAUI dependencies. No rendering. Pure orchestration.
/// </summary>
public sealed class SessionController
{
    private SessionState _state = SessionState.Ready;
    private uint _seed;
    private int _gatesTotal;
    private readonly Stopwatch _elapsed = new();
    private int _maxCombo;
    private int _currentCombo;
    private int _totalScore;
    private readonly List<GateResult> _gateResults = new(capacity: 16);
    private RunDescriptor? _runDescriptor;

    public SessionState State => _state;
    public uint Seed => _seed;
    public int TotalScore => _totalScore;
    public int CurrentCombo => _currentCombo;
    public int MaxCombo => _maxCombo;
    public TimeSpan Elapsed => _elapsed.Elapsed;
    public RunDescriptor? RunDescriptor => _runDescriptor;

    /// <summary>
    /// Reset to Ready state with the given seed and gate count.
    /// </summary>
    public void ResetToReady(uint seed, int gatesTotal)
    {
        _state = SessionState.Ready;
        _seed = seed;
        _gatesTotal = gatesTotal;
        _runDescriptor = null;
        _elapsed.Reset();
        _maxCombo = 0;
        _currentCombo = 0;
        _totalScore = 0;
        _gateResults.Clear();
    }

    /// <summary>
    /// Reset to Ready state with a full RunDescriptor for identity tracking.
    /// </summary>
    public void ResetToReady(RunDescriptor run, int gatesTotal)
    {
        _state = SessionState.Ready;
        _seed = run.Seed;
        _gatesTotal = gatesTotal;
        _runDescriptor = run;
        _elapsed.Reset();
        _maxCombo = 0;
        _currentCombo = 0;
        _totalScore = 0;
        _gateResults.Clear();
    }

    /// <summary>
    /// Transition Ready → Playing. Starts the elapsed timer.
    /// </summary>
    public void Start()
    {
        if (_state != SessionState.Ready) return;
        _state = SessionState.Playing;
        _elapsed.Start();
    }

    /// <summary>
    /// Process events emitted by the simulation during a frame.
    /// Only processes during Playing state.
    /// Returns true if the session transitioned to Results (LevelComplete received).
    /// </summary>
    public bool ApplyEvents(IReadOnlyList<GameEvent> events)
    {
        if (_state != SessionState.Playing) return false;

        foreach (var ev in events)
        {
            switch (ev.Type)
            {
                case GameEventType.EnteredGate:
                {
                    int score = ev.Arg1;
                    _totalScore += score;
                    _currentCombo++;
                    if (_currentCombo > _maxCombo) _maxCombo = _currentCombo;

                    // Reverse the encoding: intensity = 1f - normalizedOffset * 0.5f
                    float offsetNormalized = (1f - ev.Intensity) * 2f;

                    _gateResults.Add(new GateResult(
                        ev.Arg0,
                        true,
                        score,
                        offsetNormalized));
                    break;
                }

                case GameEventType.HitWall:
                {
                    _currentCombo = 0;
                    float missDistance = ev.Arg1 / 1000f;

                    _gateResults.Add(new GateResult(
                        ev.Arg0,
                        false,
                        0,
                        1f + missDistance));
                    break;
                }

                case GameEventType.LevelComplete:
                    _elapsed.Stop();
                    _state = SessionState.Results;
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Build the final immutable result. Only valid in Results state.
    /// </summary>
    public SessionResult? GetResult()
    {
        if (_state != SessionState.Results) return null;

        int gatesPassed = 0;
        foreach (var g in _gateResults)
        {
            if (g.Passed) gatesPassed++;
        }

        return new SessionResult(
            _seed,
            _elapsed.Elapsed,
            _totalScore,
            _maxCombo,
            gatesPassed,
            _gatesTotal,
            _gateResults.AsReadOnly(),
            _runDescriptor?.Id);
    }
}
