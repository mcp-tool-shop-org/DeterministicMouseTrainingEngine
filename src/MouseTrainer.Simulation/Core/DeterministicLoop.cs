using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;

namespace MouseTrainer.Simulation.Core;

/// <summary>
/// Fixed-step deterministic loop with accumulator.
/// Host provides real time; simulation advances in fixed ticks only.
/// </summary>
public sealed class DeterministicLoop
{
    private readonly IGameSimulation _sim;
    private readonly DeterministicConfig _cfg;

    private long _tick;
    private double _accumulatorSeconds;
    private long _lastHostTicks;

    private readonly float _fixedDt;
    private readonly double _fixedDtD;

    private readonly List<GameEvent> _events = new(capacity: 64);

    public DeterministicLoop(IGameSimulation sim, DeterministicConfig? cfg = null)
    {
        _sim = sim;
        _cfg = cfg ?? new DeterministicConfig();
        _fixedDt = 1f / _cfg.FixedHz;
        _fixedDtD = 1.0 / _cfg.FixedHz;

        Reset();
    }

    public void Reset()
    {
        _tick = 0;
        _accumulatorSeconds = 0;
        _lastHostTicks = 0;
        _sim.Reset(_cfg.SessionSeed);
    }

    /// <summary>
    /// Step using host-provided input and time.
    /// </summary>
    /// <param name="input">Latest sampled pointer input.</param>
    /// <param name="hostNowTicks">Host clock ticks (monotonic if possible).</param>
    /// <param name="ticksPerSecond">Host ticks per second (e.g., Stopwatch.Frequency).</param>
    public FrameResult Step(in PointerInput input, long hostNowTicks, double ticksPerSecond)
    {
        if (_lastHostTicks == 0)
        {
            _lastHostTicks = hostNowTicks;
            return new FrameResult { Tick = _tick, Events = Array.Empty<GameEvent>(), Alpha = 0f };
        }

        var deltaTicks = hostNowTicks - _lastHostTicks;
        _lastHostTicks = hostNowTicks;

        var deltaSeconds = deltaTicks / ticksPerSecond;
        if (deltaSeconds < 0) deltaSeconds = 0; // guard against clock anomalies

        _accumulatorSeconds += deltaSeconds;

        _events.Clear();

        int steps = 0;
        while (_accumulatorSeconds >= _fixedDtD && steps < _cfg.MaxStepsPerFrame)
        {
            _sim.FixedUpdate(_tick, _fixedDt, input, _events);
            _tick++;
            _accumulatorSeconds -= _fixedDtD;
            steps++;
        }

        // Alpha for rendering interpolation between ticks
        var alpha = (float)(_accumulatorSeconds / _fixedDtD);
        if (alpha < 0f) alpha = 0f;
        if (alpha >= 1f) alpha = 0.9999f;

        return new FrameResult { Tick = _tick, Events = _events.ToArray(), Alpha = alpha };
    }
}
