using MouseTrainer.Simulation.Session;

namespace MouseTrainer.MauiHost;

/// <summary>
/// Mutable state snapshot consumed by the debug overlay drawable.
/// Updated by the host each frame; read by the draw method.
/// </summary>
public sealed class DebugOverlayState
{
    // Pointer
    public float CursorX;
    public float CursorY;
    public bool PrimaryDown;

    // Mapping params (virtual → device)
    public float OffsetX;
    public float OffsetY;
    public float Scale;

    // Sim
    public long Tick;
    public int Score;
    public int Combo;

    // Gate preview (optional — populated when ISimDebugOverlay is available)
    public bool HasGate;
    public float GateWallX;
    public float GateCenterY;
    public float GateApertureHeight;
    public int GateIndex;
    public float ScrollX;

    // Session state
    public SessionState SessionPhase;
    public uint Seed;
    public SessionResult? LastResult;
}

/// <summary>
/// Debug overlay drawable: playfield bounds, cursor dot, gate preview, HUD.
/// Now session-aware: renders Ready, Playing, and Results screens.
/// Drawn on a GraphicsView layered over the game surface.
/// </summary>
public sealed class DebugOverlayDrawable : IDrawable
{
    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;

    private readonly DebugOverlayState _s;

    public DebugOverlayDrawable(DebugOverlayState state) => _s = state;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var scale = _s.Scale;
        if (scale <= 0.0001f)
            scale = MathF.Min(dirtyRect.Width / VirtualW, dirtyRect.Height / VirtualH);

        var contentW = VirtualW * scale;
        var contentH = VirtualH * scale;
        var offsetX = _s.OffsetX;
        var offsetY = _s.OffsetY;

        if (offsetX == 0f && offsetY == 0f && scale > 0f)
        {
            offsetX = (dirtyRect.Width - contentW) * 0.5f;
            offsetY = (dirtyRect.Height - contentH) * 0.5f;
        }

        // ── Ready screen ──────────────────────────────────
        if (_s.SessionPhase == SessionState.Ready)
        {
            DrawReadyScreen(canvas, dirtyRect, offsetX, offsetY, contentW, contentH);
            return;
        }

        // ── Results screen ────────────────────────────────
        if (_s.SessionPhase == SessionState.Results && _s.LastResult is { } result)
        {
            DrawResultsScreen(canvas, dirtyRect, result);
            return;
        }

        // ── Playing: normal game rendering ────────────────
        DrawPlayfield(canvas, offsetX, offsetY, contentW, contentH);
        DrawGatePreview(canvas, offsetX, offsetY, contentW, contentH, scale);
        DrawCursor(canvas, offsetX, offsetY, scale);
        DrawHud(canvas, offsetX, offsetY, contentW, contentH);
    }

    // ================================================================
    //  Ready screen
    // ================================================================

    private void DrawReadyScreen(ICanvas canvas, RectF rect, float ox, float oy, float cw, float ch)
    {
        // Playfield bounds (subtle)
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Color.FromArgb("#333333");
        canvas.StrokeDashPattern = new float[] { 4, 4 };
        canvas.DrawRectangle(ox, oy, cw, ch);
        canvas.StrokeDashPattern = null;

        // Seed
        canvas.FontSize = 20;
        canvas.FontColor = Colors.White;
        canvas.DrawString(
            $"Seed: 0x{_s.Seed:X8}",
            rect.Width * 0.5f, rect.Height * 0.4f,
            HorizontalAlignment.Center);

        // Prompt
        canvas.FontSize = 14;
        canvas.FontColor = Colors.LimeGreen;
        canvas.DrawString(
            "Press Start to begin",
            rect.Width * 0.5f, rect.Height * 0.55f,
            HorizontalAlignment.Center);

        // Mode label
        canvas.FontSize = 11;
        canvas.FontColor = Color.FromArgb("#666666");
        canvas.DrawString(
            "Reflex Gates — 12 gates",
            rect.Width * 0.5f, rect.Height * 0.63f,
            HorizontalAlignment.Center);
    }

    // ================================================================
    //  Results screen
    // ================================================================

    private static void DrawResultsScreen(ICanvas canvas, RectF rect, SessionResult r)
    {
        // Dark overlay
        canvas.FillColor = Color.FromArgb("#000000CC");
        canvas.FillRectangle(0, 0, rect.Width, rect.Height);

        var cx = rect.Width * 0.5f;
        var top = rect.Height * 0.12f;

        // Title
        canvas.FontSize = 28;
        canvas.FontColor = Colors.Gold;
        canvas.DrawString("SESSION COMPLETE", cx, top, HorizontalAlignment.Center);

        // Summary stats
        canvas.FontSize = 16;
        canvas.FontColor = Colors.White;
        canvas.DrawString($"Score: {r.TotalScore}", cx, top + 50, HorizontalAlignment.Center);
        canvas.DrawString($"Gates: {r.GatesPassed}/{r.GatesTotal}", cx, top + 74, HorizontalAlignment.Center);
        canvas.DrawString($"Max Combo: {r.MaxCombo}", cx, top + 98, HorizontalAlignment.Center);
        canvas.DrawString($"Time: {r.Elapsed.TotalSeconds:0.0}s", cx, top + 122, HorizontalAlignment.Center);
        canvas.DrawString($"Seed: 0x{r.Seed:X8}", cx, top + 146, HorizontalAlignment.Center);

        // Accuracy
        float accuracy = r.GatesTotal > 0 ? r.GatesPassed * 100f / r.GatesTotal : 0f;
        canvas.FontColor = accuracy >= 80 ? Colors.LimeGreen : accuracy >= 50 ? Colors.Yellow : Colors.OrangeRed;
        canvas.DrawString($"Accuracy: {accuracy:0}%", cx, top + 178, HorizontalAlignment.Center);

        // Per-gate breakdown header
        canvas.FontSize = 12;
        canvas.FontColor = Color.FromArgb("#999999");
        canvas.DrawString("─── Gate Breakdown ───", cx, top + 215, HorizontalAlignment.Center);

        // Per-gate list
        canvas.FontSize = 11;
        float gateY = top + 240;
        for (int i = 0; i < r.Gates.Count; i++)
        {
            var g = r.Gates[i];
            canvas.FontColor = g.Passed ? Colors.LimeGreen : Colors.OrangeRed;
            var label = g.Passed
                ? $"Gate {g.GateIndex + 1}: +{g.Score}  (offset {g.OffsetNormalized:0.00})"
                : $"Gate {g.GateIndex + 1}: MISS  (offset {g.OffsetNormalized:0.00})";
            canvas.DrawString(label, cx, gateY + i * 18, HorizontalAlignment.Center);
        }

        // Footer prompt
        canvas.FontSize = 13;
        canvas.FontColor = Color.FromArgb("#888888");
        canvas.DrawString(
            "Press Retry or New Seed",
            cx, rect.Height - 30,
            HorizontalAlignment.Center);
    }

    // ================================================================
    //  Playing: game elements
    // ================================================================

    private void DrawPlayfield(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        canvas.StrokeSize = 2;
        canvas.StrokeColor = Colors.White;
        canvas.StrokeDashPattern = new float[] { 4, 4 };
        canvas.DrawRectangle(ox, oy, cw, ch);
        canvas.StrokeDashPattern = null;
    }

    private void DrawGatePreview(ICanvas canvas, float ox, float oy, float cw, float ch, float scale)
    {
        if (!_s.HasGate) return;

        var gateScreenX = ox + (_s.GateWallX - _s.ScrollX) * scale;

        if (gateScreenX < ox - 20 || gateScreenX > ox + cw + 20) return;

        var gapHalf = _s.GateApertureHeight * 0.5f * scale;
        var gapCenterScreenY = oy + _s.GateCenterY * scale;

        // Top wall (above aperture)
        canvas.StrokeSize = 3;
        canvas.StrokeColor = Colors.Yellow;
        canvas.DrawLine(gateScreenX, oy, gateScreenX, gapCenterScreenY - gapHalf);

        // Bottom wall (below aperture)
        canvas.DrawLine(gateScreenX, gapCenterScreenY + gapHalf, gateScreenX, oy + ch);

        // Aperture edges (horizontal ticks)
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Colors.LimeGreen;
        canvas.DrawLine(gateScreenX - 8, gapCenterScreenY - gapHalf, gateScreenX + 8, gapCenterScreenY - gapHalf);
        canvas.DrawLine(gateScreenX - 8, gapCenterScreenY + gapHalf, gateScreenX + 8, gapCenterScreenY + gapHalf);

        // Gate label
        canvas.FontSize = 11;
        canvas.FontColor = Colors.Yellow;
        canvas.DrawString($"gate {_s.GateIndex}", gateScreenX + 6, oy + 16, HorizontalAlignment.Left);
    }

    private void DrawCursor(ICanvas canvas, float ox, float oy, float scale)
    {
        var cx = ox + _s.CursorX * scale;
        var cy = oy + _s.CursorY * scale;

        canvas.FillColor = _s.PrimaryDown ? Colors.LimeGreen : Colors.Cyan;
        canvas.FillCircle(cx, cy, 6);

        // Crosshair
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Colors.White;
        canvas.Alpha = 0.5f;
        canvas.DrawLine(cx - 12, cy, cx + 12, cy);
        canvas.DrawLine(cx, cy - 12, cx, cy + 12);
        canvas.Alpha = 1f;
    }

    private void DrawHud(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        canvas.FontSize = 12;
        canvas.FontColor = Colors.White;

        var hudY = oy + ch - 20;
        canvas.DrawString(
            $"tick={_s.Tick}  Y={_s.CursorY:0}  score={_s.Score}  combo={_s.Combo}",
            ox + 8, hudY, HorizontalAlignment.Left);
    }
}
