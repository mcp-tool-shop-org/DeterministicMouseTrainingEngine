using System.Diagnostics;
using MouseTrainer.Audio.Assets;
using MouseTrainer.Audio.Core;
using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;
using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Core;
using MouseTrainer.Simulation.Modes.ReflexGates;
using MouseTrainer.Simulation.Mutators;
using MouseTrainer.Simulation.Session;
using Plugin.Maui.Audio;

namespace MouseTrainer.MauiHost;

public partial class MainPage : ContentPage
{
    private readonly ReflexGateSimulation _sim;
    private readonly DeterministicLoop _loop;
    private readonly MauiAudioSink _sink;
    private readonly AudioDirector _audio;
    private readonly SessionController _session = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly RendererState _overlayState = new();
    private readonly int _fixedHz = 60;
    private readonly int _gateCount;

    // --- Effects systems ---
    private readonly TrailBuffer _trailBuffer = new(16);
    private readonly ParticleSystem _particles = new(32);
    private readonly ScreenShake _shake = new();

    // --- Live gate results (for rendering pass/miss before session ends) ---
    private readonly List<GateResult> _liveGateResults = new(16);

    // --- Persistence ---
    private readonly SessionStore _store;

    private readonly ReflexGateGenerator _generator;
    private readonly MutatorPipeline _mutatorPipeline;
    private IDispatcherTimer? _timer;
    private long _frame;
    private uint _currentSeed = 0xC0FFEEu;
    private RunDescriptor _currentRun;

    // --- Pointer state (sampled by host, consumed by sim) ---
    private float _latestX;
    private float _latestY;
    private bool _primaryDown;

    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;

    public MainPage()
    {
        InitializeComponent();

        var cfg = new ReflexGateConfig();
        _gateCount = cfg.GateCount;
        _sim = new ReflexGateSimulation(cfg);
        _generator = new ReflexGateGenerator(cfg);

        var mutatorRegistry = new MutatorRegistry();
        mutatorRegistry.Register(MutatorId.NarrowMargin, 1, spec => new NarrowMarginMutator(spec));
        mutatorRegistry.Register(MutatorId.WideMargin, 1, spec => new WideMarginMutator(spec));
        mutatorRegistry.Register(MutatorId.DifficultyCurve, 1, spec => new DifficultyCurveMutator(spec));
        _mutatorPipeline = new MutatorPipeline(mutatorRegistry);

        _currentRun = RunDescriptor.Create(ModeId.ReflexGates, _currentSeed);
        _loop = new DeterministicLoop(_sim, new DeterministicConfig
        {
            FixedHz = _fixedHz,
            MaxStepsPerFrame = 6,
            SessionSeed = _currentSeed
        });

        _sink = new MauiAudioSink(AudioManager.Current, log: AppendLog);
        _audio = new AudioDirector(AudioCueMap.Default(), _sink);
        _store = new SessionStore(log: AppendLog);

        // Wire effects systems into renderer state
        _overlayState.Trail = _trailBuffer;
        _overlayState.Particles = _particles;
        _overlayState.GateCount = _gateCount;

        OverlayView.Drawable = new GameRenderer(_overlayState);
        AttachPointerInput();

        // Initialize session to Ready
        _session.ResetToReady(_currentRun, _gateCount);
        _overlayState.SessionPhase = SessionState.Ready;
        _overlayState.Seed = _currentSeed;
        PopulateGatesAtRest();
        SeedLabel.Text = $"Seed: 0x{_currentSeed:X8}";
        OverlayView.Invalidate();

        _ = VerifyAssetsAsync();
        _ = LoadStatsAsync();
        AppendLog($"> Host started. FixedHz={_fixedHz}  Seed=0x{_currentSeed:X8}");
    }

    // ------------------------------------------------------------------
    //  Pointer input
    // ------------------------------------------------------------------

    private void AttachPointerInput()
    {
        var ptr = new PointerGestureRecognizer();

        ptr.PointerMoved += (_, e) =>
        {
            var p = e.GetPosition(GameSurface);
            if (p is null) return;
            (_latestX, _latestY) = DeviceToVirtual((float)p.Value.X, (float)p.Value.Y);

            _overlayState.CursorX = _latestX;
            _overlayState.CursorY = _latestY;
            OverlayView.Invalidate();
        };

        ptr.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(GameSurface);
            if (p is not null)
            {
                (_latestX, _latestY) = DeviceToVirtual((float)p.Value.X, (float)p.Value.Y);
                _overlayState.CursorX = _latestX;
                _overlayState.CursorY = _latestY;
            }
            _primaryDown = true;
            _overlayState.PrimaryDown = true;
            OverlayView.Invalidate();
        };

        ptr.PointerReleased += (_, _) =>
        {
            _primaryDown = false;
            _overlayState.PrimaryDown = false;
            OverlayView.Invalidate();
        };

        GameSurface.GestureRecognizers.Add(ptr);
    }

    private (float X, float Y) DeviceToVirtual(float deviceX, float deviceY)
    {
        var w = (float)GameSurface.Width;
        var h = (float)GameSurface.Height;

        if (w <= 1 || h <= 1)
            return (0f, 0f);

        var scale = MathF.Min(w / VirtualW, h / VirtualH);
        var contentW = VirtualW * scale;
        var contentH = VirtualH * scale;
        var offsetX = (w - contentW) * 0.5f;
        var offsetY = (h - contentH) * 0.5f;

        _overlayState.OffsetX = offsetX;
        _overlayState.OffsetY = offsetY;
        _overlayState.Scale = scale;

        var x = (deviceX - offsetX) / scale;
        var y = (deviceY - offsetY) / scale;

        x = MathF.Max(0f, MathF.Min(VirtualW, x));
        y = MathF.Max(0f, MathF.Min(VirtualH, y));

        return (x, y);
    }

    private PointerInput SamplePointer()
        => new PointerInput(_latestX, _latestY, _primaryDown, false, _stopwatch.ElapsedTicks);

    // ------------------------------------------------------------------
    //  Session flow: Ready -> Playing -> Results
    // ------------------------------------------------------------------

    private void OnActionClicked(object sender, EventArgs e)
    {
        switch (_session.State)
        {
            case SessionState.Ready:
                StartSession();
                break;

            case SessionState.Playing:
                break;

            case SessionState.Results:
                ResetSession(_currentSeed);
                StartSession();
                break;
        }
    }

    private void OnNewSeedClicked(object sender, EventArgs e)
    {
        StopTimer();
        _currentSeed = (uint)Environment.TickCount;
        _currentRun = RunDescriptor.Create(ModeId.ReflexGates, _currentSeed);
        ResetSession(_currentSeed);
    }

    private void StartSession()
    {
        _currentRun = RunDescriptor.Create(ModeId.ReflexGates, _currentSeed);
        _loop.Reset(_currentSeed);
        _session.ResetToReady(_currentRun, _gateCount);
        _session.Start();

        _frame = 0;
        _liveGateResults.Clear();
        _trailBuffer.Clear();
        _particles.Clear();
        _shake.Clear();
        _overlayState.ActiveFlashes.Clear();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => StepOnce();
        _timer.Start();

        _overlayState.SessionPhase = SessionState.Playing;
        _overlayState.Score = 0;
        _overlayState.Combo = 0;
        _overlayState.LastResult = null;

        // Start ambient music loop
        _sink.StartLoop(new AudioCue("amb_zen_loop.wav", Volume: 0.25f, Loop: true), "amb");

        ActionButton.Text = "Playing...";
        ActionButton.IsEnabled = false;
        ActionButton.BackgroundColor = Color.FromArgb("#666666");
        StatusLabel.Text = "Playing";
        StatusLabel.TextColor = Color.FromArgb("#4CAF50");

        AppendLog($"> Session started. Seed=0x{_currentSeed:X8}");
    }

    private void ResetSession(uint seed)
    {
        StopTimer();
        _sink.StopLoop("amb");

        _currentSeed = seed;
        _currentRun = RunDescriptor.Create(ModeId.ReflexGates, seed);
        _session.ResetToReady(_currentRun, _gateCount);
        _loop.Reset(seed);

        _liveGateResults.Clear();
        _trailBuffer.Clear();
        _particles.Clear();
        _shake.Clear();
        _overlayState.ActiveFlashes.Clear();

        _overlayState.SessionPhase = SessionState.Ready;
        _overlayState.Seed = seed;
        _overlayState.Score = 0;
        _overlayState.Combo = 0;
        _overlayState.LastResult = null;
        _overlayState.Bests = _store.GetPersonalBests(currentSeed: seed);
        _overlayState.Lifetime = _store.GetLifetimeStats();
        PopulateGatesAtRest();

        ActionButton.Text = "Start";
        ActionButton.IsEnabled = true;
        ActionButton.BackgroundColor = Color.FromArgb("#4CAF50");
        StatusLabel.Text = "Ready";
        StatusLabel.TextColor = Color.FromArgb("#888888");
        SeedLabel.Text = $"Seed: 0x{seed:X8}";

        OverlayView.Invalidate();
        AppendLog($"> Reset. Seed=0x{seed:X8}");
    }

    private void StopTimer()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }

    // ------------------------------------------------------------------
    //  Gate data helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Populate all gates at rest positions (simTime=0) for the Ready screen preview.
    /// </summary>
    private void PopulateGatesAtRest()
    {
        _overlayState.Gates.Clear();
        _overlayState.ScrollPosition = 0f;

        for (int i = 0; i < _sim.Gates.Count; i++)
        {
            var gate = _sim.Gates[i];
            float difficulty = _sim.Gates.Count <= 1 ? 0f : (float)i / (_sim.Gates.Count - 1);
            _overlayState.Gates.Add(new GateRenderData(
                WallX: gate.WallX,
                CenterY: gate.CurrentCenterY(0f),
                ApertureHeight: gate.ApertureHeight,
                GateIndex: i,
                Difficulty: difficulty,
                Status: GateVisualStatus.Upcoming));
        }
    }

    /// <summary>
    /// Populate all gates with live positions and pass/miss status.
    /// </summary>
    private void PopulateGatesLive(float simTime)
    {
        _overlayState.Gates.Clear();
        _overlayState.ScrollPosition = _sim.ScrollPosition;

        for (int i = 0; i < _sim.Gates.Count; i++)
        {
            var gate = _sim.Gates[i];
            float difficulty = _sim.Gates.Count <= 1 ? 0f : (float)i / (_sim.Gates.Count - 1);

            GateVisualStatus status;
            if (i < _sim.NextGateIndex)
            {
                status = (i < _liveGateResults.Count && _liveGateResults[i].Passed)
                    ? GateVisualStatus.Passed
                    : GateVisualStatus.Missed;
            }
            else
            {
                status = GateVisualStatus.Upcoming;
            }

            _overlayState.Gates.Add(new GateRenderData(
                WallX: gate.WallX,
                CenterY: gate.CurrentCenterY(simTime),
                ApertureHeight: gate.ApertureHeight,
                GateIndex: i,
                Difficulty: difficulty,
                Status: status));
        }
    }

    // ------------------------------------------------------------------
    //  Simulation loop
    // ------------------------------------------------------------------

    private void StepOnce()
    {
        if (_session.State != SessionState.Playing) return;

        var input = SamplePointer();
        var nowTicks = _stopwatch.ElapsedTicks;
        var result = _loop.Step(input, nowTicks, Stopwatch.Frequency);

        float simTime = result.Tick * (1f / _fixedHz);
        float dt = 1f / _fixedHz;

        // Process events through session controller + audio + effects
        if (result.Events.Count > 0)
        {
            // Track gate results for live rendering + spawn effects
            foreach (var ev in result.Events)
            {
                if (ev.Type == GameEventType.EnteredGate)
                {
                    float offset = (1f - ev.Intensity) * 2f;
                    _liveGateResults.Add(new GateResult(ev.Arg0, true, ev.Arg1, offset));

                    // Spawn pass burst + flash
                    int gateIdx = ev.Arg0;
                    if (gateIdx < _sim.Gates.Count)
                    {
                        var gate = _sim.Gates[gateIdx];
                        float cy = gate.CurrentCenterY(simTime);
                        float diff = _sim.Gates.Count <= 1 ? 0f
                            : (float)gateIdx / (_sim.Gates.Count - 1);
                        var color = NeonPalette.GateDifficultyColor(diff);
                        _particles.SpawnPassBurst(gate.WallX, cy, color);
                        _overlayState.ActiveFlashes.Add(new RendererState.GateFlash
                        {
                            CenterX = gate.WallX,
                            CenterY = cy,
                            Remaining = 0.2f,
                            Color = color
                        });
                    }
                }
                else if (ev.Type == GameEventType.HitWall)
                {
                    float miss = ev.Arg1 / 1000f;
                    _liveGateResults.Add(new GateResult(ev.Arg0, false, 0, 1f + miss));

                    // Spawn miss burst + shake
                    int gateIdx = ev.Arg0;
                    if (gateIdx < _sim.Gates.Count)
                    {
                        var gate = _sim.Gates[gateIdx];
                        float cy = gate.CurrentCenterY(simTime);
                        _particles.SpawnMissBurst(gate.WallX, cy);
                        _shake.Trigger(amplitude: 6f + ev.Intensity * 4f, duration: 0.12f);
                    }
                }
            }

            bool transitioned = _session.ApplyEvents(result.Events);
            _audio.Process(result.Events, result.Tick, sessionSeed: _currentSeed);

            if (transitioned)
            {
                StopTimer();
                _sink.StopLoop("amb");

                _overlayState.SessionPhase = SessionState.Results;
                _overlayState.LastResult = _session.GetResult();

                // Persist session + load PBs for results screen
                if (_overlayState.LastResult is { } finalResult)
                {
                    // Get PBs *before* saving so "new record" detection works correctly
                    _overlayState.Bests = _store.GetPersonalBests(currentSeed: _currentSeed);
                    _overlayState.Lifetime = _store.GetLifetimeStats();
                    _ = _store.SaveSessionAsync(finalResult);
                }

                ActionButton.Text = "Retry";
                ActionButton.IsEnabled = true;
                ActionButton.BackgroundColor = Color.FromArgb("#4CAF50");
                StatusLabel.Text = "Complete";
                StatusLabel.TextColor = Color.FromArgb("#FFD700");

                OverlayView.Invalidate();
                AppendLog($"> Session complete! Score={_session.TotalScore}  MaxCombo={_session.MaxCombo}  Time={_session.Elapsed.TotalSeconds:0.0}s");
                return;
            }
        }

        // Update renderer state
        _overlayState.Tick = result.Tick;
        _overlayState.SimTime = simTime;
        _overlayState.Alpha = result.Alpha;
        _overlayState.Score = _session.TotalScore;
        _overlayState.Combo = _session.CurrentCombo;

        // Populate all gates with live positions
        PopulateGatesLive(simTime);

        // Push cursor trail point
        _trailBuffer.Push(_latestX, _latestY, simTime);

        // Update effects systems
        _particles.Update(dt);
        _shake.Update(dt, _overlayState);

        // Decay gate flashes
        for (int i = _overlayState.ActiveFlashes.Count - 1; i >= 0; i--)
        {
            var f = _overlayState.ActiveFlashes[i];
            f.Remaining -= dt;
            if (f.Remaining <= 0f)
                _overlayState.ActiveFlashes.RemoveAt(i);
            else
                _overlayState.ActiveFlashes[i] = f;
        }

        _frame++;
        if (_frame % 2 == 0)
            OverlayView.Invalidate();

        if (_frame % 60 == 0)
            AppendLog($"> tick={result.Tick} Y={_latestY:0} score={_session.TotalScore} combo={_session.CurrentCombo}");
    }

    // ------------------------------------------------------------------
    //  Assets
    // ------------------------------------------------------------------

    private async Task LoadStatsAsync()
    {
        await _store.LoadAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _overlayState.Bests = _store.GetPersonalBests(currentSeed: _currentSeed);
            _overlayState.Lifetime = _store.GetLifetimeStats();
            OverlayView.Invalidate();
        });
    }

    private async Task VerifyAssetsAsync()
    {
        try
        {
            var missing = await AssetVerifier.VerifyRequiredAudioAsync(new MauiAssetOpener(), CancellationToken.None);
            if (missing.Count == 0)
                AppendLog("> Assets OK.");
            else
            {
                AppendLog($"> MISSING {missing.Count} assets:");
                foreach (var m in missing) AppendLog($"  - {m}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"> Asset error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void AppendLog(string line)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogLabel.Text += line + Environment.NewLine;
        });
    }
}
