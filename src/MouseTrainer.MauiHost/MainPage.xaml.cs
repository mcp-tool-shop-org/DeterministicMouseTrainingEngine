using System.Diagnostics;
using MouseTrainer.Audio.Assets;
using MouseTrainer.Audio.Core;
using MouseTrainer.Domain.Input;
using MouseTrainer.Simulation.Core;
using MouseTrainer.Simulation.Debug;
using MouseTrainer.Simulation.Modes.ReflexGates;

namespace MouseTrainer.MauiHost;

public partial class MainPage : ContentPage
{
    private readonly IGameSimulation _sim;
    private readonly DeterministicLoop _loop;
    private readonly AudioDirector _audio;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly DebugOverlayState _overlayState = new();
    private readonly int _fixedHz = 60;

    private IDispatcherTimer? _timer;
    private long _frame;

    // --- Pointer state (sampled by host, consumed by sim) ---
    private float _latestX;
    private float _latestY;
    private bool _primaryDown;

    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;

    public MainPage()
    {
        InitializeComponent();

        _sim = new ReflexGateSimulation(new ReflexGateConfig());
        _loop = new DeterministicLoop(_sim, new DeterministicConfig
        {
            FixedHz = _fixedHz,
            MaxStepsPerFrame = 6,
            SessionSeed = 0xC0FFEEu
        });

        _audio = new AudioDirector(AudioCueMap.Default(), new LogAudioSink(AppendLog));

        OverlayView.Drawable = new DebugOverlayDrawable(_overlayState);
        AttachPointerInput();
        _ = VerifyAssetsAsync();
        AppendLog($"> Host started. FixedHz={_fixedHz}");
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
    //  Simulation loop
    // ------------------------------------------------------------------

    private void OnStartClicked(object sender, EventArgs e)
    {
        if (_timer is not null) return;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => StepOnce();
        _timer.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusLabel.Text = "Running";
        StatusLabel.TextColor = Color.FromArgb("#4CAF50");

        AppendLog("> Loop started.");
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        if (_timer is null) return;

        _timer.Stop();
        _timer = null;

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusLabel.Text = "Stopped";
        StatusLabel.TextColor = Color.FromArgb("#888888");

        AppendLog("> Loop stopped.");
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        LogLabel.Text = "";
    }

    private void StepOnce()
    {
        var input = SamplePointer();
        var nowTicks = _stopwatch.ElapsedTicks;
        var result = _loop.Step(input, nowTicks, Stopwatch.Frequency);

        // Drive audio from events
        if (result.Events.Count > 0)
            _audio.Process(result.Events, result.Tick, sessionSeed: 0xC0FFEEu);

        // Update overlay state
        _overlayState.Tick = result.Tick;

        // Pull score/combo from sim if it exposes them
        if (_sim is ReflexGateSimulation rgs)
        {
            _overlayState.Score = rgs.TotalScore;
            _overlayState.Combo = rgs.ComboStreak;
            _overlayState.LevelComplete = rgs.IsLevelComplete;
        }

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
            AppendLog($"> tick={result.Tick} Y={_latestY:0} score={_overlayState.Score} combo={_overlayState.Combo}");
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
