using System.Diagnostics;
using MouseTrainer.Core.Assets;
using MouseTrainer.Core.Audio;
using MouseTrainer.Core.Input;
using MouseTrainer.Core.Simulation;
using MouseTrainer.Core.Simulation.ReflexGates;

namespace MouseTrainer.MauiHost;

public partial class MainPage : ContentPage
{
    private readonly DeterministicLoop _loop;
    private readonly AudioDirector _audio;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private IDispatcherTimer? _timer;
    private long _frame;

    public MainPage()
    {
        InitializeComponent();

        // Deterministic foundation only:
        _loop = new DeterministicLoop(new ReflexGateSimulation(new ReflexGateConfig()), new DeterministicConfig
        {
            FixedHz = 60,
            MaxStepsPerFrame = 6,
            SessionSeed = 0xC0FFEEu
        });

        _audio = new AudioDirector(AudioCueMap.Default(), new LogAudioSink(AppendLog));

        _ = VerifyAssetsAsync();
        AppendLog($"> Host started. Stopwatch frequency: {Stopwatch.Frequency} ticks/sec");
        AppendLog($"> Required audio assets: {AssetManifest.RequiredAudio.Length}");
    }

    private async Task VerifyAssetsAsync()
    {
        try
        {
            var missing = await AssetVerifier.VerifyRequiredAudioAsync(new MauiAssetOpener(), CancellationToken.None);
            if (missing.Count == 0)
            {
                AppendLog($"> Asset check OK. All required audio assets are present.");
            }
            else
            {
                AppendLog($"> ASSET CHECK FAILED. Missing:");
                foreach (var m in missing) AppendLog($"  - {m}");
                AppendLog($"> Fix: ensure these files exist under Resources/Raw and are included as MauiAsset.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"> ASSET CHECK ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnStartClicked(object sender, EventArgs e)
    {
        if (_timer is not null) return;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16); // host tick rate (not sim rate)
        _timer.Tick += (_, __) => StepOnce();
        _timer.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;

        AppendLog($"> Loop started.");
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        if (_timer is null) return;

        _timer.Stop();
        _timer = null;

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;

        AppendLog($"> Loop stopped.");
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        LogLabel.Text = "";
    }

    private void StepOnce()
    {
        // Minimal pointer sample (no gameplay yet). In a real host you'd wire pointer events.
        var input = new PointerInput(0f, 0f, false, false, _stopwatch.ElapsedTicks);

        var nowTicks = _stopwatch.ElapsedTicks;
        var result = _loop.Step(input, nowTicks, Stopwatch.Frequency);

        // Drive deterministic audio from deterministic events
        if (result.Events.Count > 0)
            _audio.Process(result.Events, result.Tick, sessionSeed: 0xC0FFEEu);

        // Keep log light
        _frame++;
        if (_frame % 30 == 0)
        {
            AppendLog($"> tick={result.Tick} events={result.Events.Count} alpha={result.Alpha:0.000}");
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
