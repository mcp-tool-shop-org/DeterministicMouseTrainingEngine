using System.Diagnostics;
using MouseTrainer.Audio.Assets;
using MouseTrainer.Audio.Core;
using MouseTrainer.Domain.Input;
using MouseTrainer.Simulation.Core;
using MouseTrainer.Simulation.Debug;
using MouseTrainer.Simulation.Modes.ReflexGates;
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

    private readonly DebugOverlayState _overlayState = new();
    private readonly int _fixedHz = 60;
    private readonly int _gateCount;

    private IDispatcherTimer? _timer;
    private long _frame;
    private uint _currentSeed = 0xC0FFEEu;

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
        _loop = new DeterministicLoop(_sim, new DeterministicConfig
        {
            FixedHz = _fixedHz,
            MaxStepsPerFrame = 6,
            SessionSeed = _currentSeed
        });

        _sink = new MauiAudioSink(AudioManager.Current, log: AppendLog);
        _audio = new AudioDirector(AudioCueMap.Default(), _sink);

        OverlayView.Drawable = new DebugOverlayDrawable(_overlayState);
        AttachPointerInput();

        // Initialize session to Ready
        _session.ResetToReady(_currentSeed, _gateCount);
        _overlayState.SessionPhase = SessionState.Ready;
        _overlayState.Seed = _currentSeed;
        SeedLabel.Text = $"Seed: 0x{_currentSeed:X8}";
        OverlayView.Invalidate();

        _ = VerifyAssetsAsync();
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

        // Store mapping params for overlay drawing
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
    //  Session flow: Ready → Playing → Results
    // ------------------------------------------------------------------

    private void OnActionClicked(object sender, EventArgs e)
    {
        switch (_session.State)
        {
            case SessionState.Ready:
                StartSession();
                break;

            case SessionState.Playing:
                // No manual stop — session ends at LevelComplete
                break;

            case SessionState.Results:
                // Retry with same seed
                ResetSession(_currentSeed);
                StartSession();
                break;
        }
    }

    private void OnNewSeedClicked(object sender, EventArgs e)
    {
        StopTimer();
        _currentSeed = (uint)Environment.TickCount;
        ResetSession(_currentSeed);
    }

    private void StartSession()
    {
        _loop.Reset(_currentSeed);
        _session.ResetToReady(_currentSeed, _gateCount);
        _session.Start();

        _frame = 0;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => StepOnce();
        _timer.Start();

        _overlayState.SessionPhase = SessionState.Playing;
        _overlayState.Score = 0;
        _overlayState.Combo = 0;
        _overlayState.HasGate = false;
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
        _session.ResetToReady(seed, _gateCount);

        _overlayState.SessionPhase = SessionState.Ready;
        _overlayState.Seed = seed;
        _overlayState.Score = 0;
        _overlayState.Combo = 0;
        _overlayState.HasGate = false;
        _overlayState.LastResult = null;

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
    //  Simulation loop
    // ------------------------------------------------------------------

    private void StepOnce()
    {
        if (_session.State != SessionState.Playing) return;

        var input = SamplePointer();
        var nowTicks = _stopwatch.ElapsedTicks;
        var result = _loop.Step(input, nowTicks, Stopwatch.Frequency);

        // Process events through session controller + audio
        if (result.Events.Count > 0)
        {
            bool transitioned = _session.ApplyEvents(result.Events);
            _audio.Process(result.Events, result.Tick, sessionSeed: _currentSeed);

            if (transitioned)
            {
                // Session complete — stop the loop and ambient, show results
                StopTimer();
                _sink.StopLoop("amb");

                _overlayState.SessionPhase = SessionState.Results;
                _overlayState.LastResult = _session.GetResult();

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

        // Update overlay state from session controller
        _overlayState.Tick = result.Tick;
        _overlayState.Score = _session.TotalScore;
        _overlayState.Combo = _session.CurrentCombo;

        // Gate preview via optional debug interface
        float simTime = result.Tick * (1f / _fixedHz);
        if (_sim is ISimDebugOverlay dbg && dbg.TryGetGatePreview(simTime, out var gate))
        {
            _overlayState.HasGate = true;
            _overlayState.GateWallX = gate.WallX;
            _overlayState.GateCenterY = gate.CenterY;
            _overlayState.GateApertureHeight = gate.ApertureHeight;
            _overlayState.GateIndex = gate.GateIndex;
            _overlayState.ScrollX = gate.ScrollX;
        }
        else
        {
            _overlayState.HasGate = false;
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
