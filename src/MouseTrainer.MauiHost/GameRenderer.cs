using MouseTrainer.Simulation.Session;

namespace MouseTrainer.MauiHost;

// ═══════════════════════════════════════════════════════════════
//  Data types
// ═══════════════════════════════════════════════════════════════

public enum GateVisualStatus { Upcoming, Passed, Missed }

/// <summary>
/// Pre-computed rendering data for a single gate. Computed by MainPage each frame.
/// All positions in virtual coordinate space (1920x1080).
/// </summary>
public readonly record struct GateRenderData(
    float WallX,
    float CenterY,
    float ApertureHeight,
    int GateIndex,
    float Difficulty,
    GateVisualStatus Status);

/// <summary>
/// Mutable state snapshot consumed by the renderer. Updated by the host each frame.
/// </summary>
public sealed class RendererState
{
    // ── Pointer ──────────────────────────────────────────
    public float CursorX;
    public float CursorY;
    public bool PrimaryDown;

    // ── Mapping (virtual → device) ───────────────────────
    public float OffsetX;
    public float OffsetY;
    public float Scale;

    // ── Sim time ─────────────────────────────────────────
    public long Tick;
    public float SimTime;
    public float Alpha;

    // ── Session ──────────────────────────────────────────
    public SessionState SessionPhase;
    public uint Seed;
    public int Score;
    public int Combo;
    public int GateCount;
    public SessionResult? LastResult;

    // ── Gates (ALL visible) ──────────────────────────────
    public float ScrollPosition;
    public readonly List<GateRenderData> Gates = new(16);

    // ── Cursor trail (step 3A.2) ─────────────────────────
    public TrailBuffer? Trail;

    // ── Particles (step 3A.3) ────────────────────────────
    public ParticleSystem? Particles;

    // ── Screen shake (step 3A.3) ─────────────────────────
    public float ShakeOffsetX;
    public float ShakeOffsetY;

    // ── Gate flash effects (step 3A.3) ───────────────────
    public struct GateFlash
    {
        public float CenterX, CenterY;
        public float Remaining;
        public Color Color;
    }
    public readonly List<GateFlash> ActiveFlashes = new(4);

    // ── Stats (populated on session complete + ready screen) ──
    public PersonalBests? Bests;
    public LifetimeStats? Lifetime;
}

// ═══════════════════════════════════════════════════════════════
//  GameRenderer : IDrawable
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Neon-minimal game renderer. Replaces the debug overlay with instrument-grade visuals.
/// Consumes RendererState populated by the host each frame.
/// </summary>
public sealed class GameRenderer : IDrawable
{
    private const float VW = 1920f;
    private const float VH = 1080f;

    private readonly RendererState _s;

    public GameRenderer(RendererState state) => _s = state;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ComputeTransform(dirtyRect, out float scale, out float ox, out float oy,
                         out float cw, out float ch);

        switch (_s.SessionPhase)
        {
            case SessionState.Ready:
                DrawReadyScreen(canvas, dirtyRect, ox, oy, cw, ch, scale);
                return;
            case SessionState.Results when _s.LastResult is { } r:
                DrawResultsScreen(canvas, dirtyRect, r, _s.Bests, _s.Lifetime);
                return;
        }

        // ── Playing state (10-layer draw order) ──────────
        float shakeOx = ox + _s.ShakeOffsetX;
        float shakeOy = oy + _s.ShakeOffsetY;

        DrawBackground(canvas, shakeOx, shakeOy, cw, ch);
        DrawScanlines(canvas, shakeOx, shakeOy, cw, ch);
        DrawCorridorBounds(canvas, shakeOx, shakeOy, cw, ch);
        DrawParallaxLayers(canvas, shakeOx, shakeOy, cw, ch, scale);
        DrawGates(canvas, shakeOx, shakeOy, cw, ch, scale);
        DrawGateFlashes(canvas, shakeOx, shakeOy, scale);
        _s.Particles?.Draw(canvas, shakeOx, shakeOy, scale);
        DrawCursorTrail(canvas, shakeOx, shakeOy, scale);
        DrawCursor(canvas, shakeOx, shakeOy, scale);
        DrawHud(canvas, ox, oy, cw, ch, scale); // HUD never shaken
    }

    // ══════════════════════════════════════════════════════
    //  Transform
    // ══════════════════════════════════════════════════════

    private void ComputeTransform(RectF rect, out float scale,
        out float ox, out float oy, out float cw, out float ch)
    {
        scale = _s.Scale;
        if (scale <= 0.0001f)
            scale = MathF.Min(rect.Width / VW, rect.Height / VH);

        cw = VW * scale;
        ch = VH * scale;
        ox = _s.OffsetX;
        oy = _s.OffsetY;

        if (ox == 0f && oy == 0f && scale > 0f)
        {
            ox = (rect.Width - cw) * 0.5f;
            oy = (rect.Height - ch) * 0.5f;
        }
    }

    // ══════════════════════════════════════════════════════
    //  Ready screen
    // ══════════════════════════════════════════════════════

    private void DrawReadyScreen(ICanvas canvas, RectF rect,
        float ox, float oy, float cw, float ch, float scale)
    {
        DrawBackground(canvas, ox, oy, cw, ch);
        DrawScanlines(canvas, ox, oy, cw, ch);
        DrawCorridorBounds(canvas, ox, oy, cw, ch);

        // Dim gate preview (all gates at rest)
        canvas.Alpha = 0.15f;
        foreach (var gate in _s.Gates)
        {
            float screenX = ox + (gate.WallX - _s.ScrollPosition) * scale;
            if (screenX < ox - 20 || screenX > ox + cw + 20) continue;

            float gapHalf = gate.ApertureHeight * 0.5f * scale;
            float centerY = oy + gate.CenterY * scale;

            canvas.StrokeSize = 1;
            canvas.StrokeColor = NeonPalette.TextMuted;
            canvas.DrawLine(screenX, oy, screenX, centerY - gapHalf);
            canvas.DrawLine(screenX, centerY + gapHalf, screenX, oy + ch);
        }
        canvas.Alpha = 1f;

        // Seed display
        canvas.FontSize = 22;
        canvas.FontColor = NeonPalette.Cyan;
        canvas.DrawString($"SEED 0x{_s.Seed:X8}",
            rect.Width * 0.5f, rect.Height * 0.38f, HorizontalAlignment.Center);

        // Mode label
        canvas.FontSize = 12;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString($"REFLEX GATES \u2014 {_s.GateCount} GATES",
            rect.Width * 0.5f, rect.Height * 0.45f, HorizontalAlignment.Center);

        // Seed history teaser
        if (_s.Bests?.SeedBestScore is { } seedBest)
        {
            canvas.FontSize = 11;
            canvas.FontColor = NeonPalette.TextDim;
            canvas.DrawString($"Best on this seed: {seedBest}",
                rect.Width * 0.5f, rect.Height * 0.50f, HorizontalAlignment.Center);
        }

        // Pulsing "PRESS START"
        float pulse = 0.4f + 0.6f * MathF.Abs(MathF.Sin((float)Environment.TickCount64 * 0.003f));
        canvas.FontSize = 18;
        canvas.FontColor = NeonPalette.Lime.WithAlpha(pulse);
        canvas.DrawString("PRESS START",
            rect.Width * 0.5f, rect.Height * 0.58f, HorizontalAlignment.Center);
    }

    // ══════════════════════════════════════════════════════
    //  Results screen
    // ══════════════════════════════════════════════════════

    private static void DrawResultsScreen(ICanvas canvas, RectF rect, SessionResult r,
        PersonalBests? bests, LifetimeStats? lifetime)
    {
        // Dark overlay
        canvas.FillColor = NeonPalette.BgDeep.WithAlpha(0.92f);
        canvas.FillRectangle(0, 0, rect.Width, rect.Height);

        float cx = rect.Width * 0.5f;
        float top = rect.Height * 0.10f;

        // Title
        canvas.FontSize = 28;
        canvas.FontColor = NeonPalette.Cyan;
        canvas.DrawString("SESSION COMPLETE", cx, top, HorizontalAlignment.Center);

        // Divider
        canvas.StrokeSize = 1;
        canvas.StrokeColor = NeonPalette.CyanDim;
        canvas.DrawLine(cx - 120, top + 26, cx + 120, top + 26);

        // Score (large)
        canvas.FontSize = 36;
        canvas.FontColor = NeonPalette.TextPrimary;
        canvas.DrawString($"{r.TotalScore}", cx, top + 60, HorizontalAlignment.Center);

        canvas.FontSize = 10;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString("SCORE", cx, top + 82, HorizontalAlignment.Center);

        // Stats row
        canvas.FontSize = 14;
        canvas.FontColor = NeonPalette.TextPrimary;
        float statsY = top + 110;
        canvas.DrawString($"Gates: {r.GatesPassed}/{r.GatesTotal}",
            cx - 100, statsY, HorizontalAlignment.Center);
        canvas.DrawString($"Max Combo: {r.MaxCombo}",
            cx + 100, statsY, HorizontalAlignment.Center);
        canvas.DrawString($"Time: {r.Elapsed.TotalSeconds:0.0}s",
            cx, statsY + 22, HorizontalAlignment.Center);

        // Accuracy
        float accuracy = r.GatesTotal > 0 ? r.GatesPassed * 100f / r.GatesTotal : 0f;
        canvas.FontSize = 20;
        canvas.FontColor = accuracy >= 80 ? NeonPalette.Lime
                         : accuracy >= 50 ? NeonPalette.Amber
                         : NeonPalette.RedMagenta;
        canvas.DrawString($"{accuracy:0}%", cx, statsY + 52, HorizontalAlignment.Center);

        canvas.FontSize = 10;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString("ACCURACY", cx, statsY + 70, HorizontalAlignment.Center);

        // ── Personal bests comparison ───────────────────────
        float pbY = statsY + 90;
        if (bests != null && bests.BestScore > 0)
        {
            // Check for new records (compare against PB *before* this session was saved)
            bool isNewOverallRecord = r.TotalScore >= bests.BestScore;
            bool isNewSeedRecord = bests.SeedBestScore.HasValue
                && r.TotalScore >= bests.SeedBestScore.Value;

            if (isNewOverallRecord)
            {
                float recordPulse = 0.5f + 0.5f * MathF.Abs(MathF.Sin(
                    (float)Environment.TickCount64 * 0.004f));
                canvas.FontSize = 14;
                canvas.FontColor = NeonPalette.Lime.WithAlpha(recordPulse);
                canvas.DrawString("\u2605 NEW RECORD \u2605", cx, pbY, HorizontalAlignment.Center);
                pbY += 18;
            }
            else
            {
                canvas.FontSize = 11;
                canvas.FontColor = NeonPalette.TextDim;
                canvas.DrawString($"PERSONAL BEST: {bests.BestScore}", cx, pbY,
                    HorizontalAlignment.Center);
                pbY += 16;
            }

            if (bests.SeedBestScore.HasValue)
            {
                if (isNewSeedRecord && !isNewOverallRecord)
                {
                    float seedPulse = 0.5f + 0.5f * MathF.Abs(MathF.Sin(
                        (float)Environment.TickCount64 * 0.003f));
                    canvas.FontSize = 12;
                    canvas.FontColor = NeonPalette.Cyan.WithAlpha(seedPulse);
                    canvas.DrawString("\u2605 SEED RECORD \u2605", cx, pbY,
                        HorizontalAlignment.Center);
                }
                else if (!isNewSeedRecord)
                {
                    canvas.FontSize = 10;
                    canvas.FontColor = NeonPalette.TextMuted;
                    canvas.DrawString($"SEED BEST: {bests.SeedBestScore.Value}", cx, pbY,
                        HorizontalAlignment.Center);
                }
            }
            pbY += 18;
        }
        else
        {
            pbY += 10; // Small gap when no history yet
        }

        // Per-gate breakdown
        canvas.FontSize = 11;
        float gateY = pbY;
        for (int i = 0; i < r.Gates.Count; i++)
        {
            var g = r.Gates[i];
            canvas.FontColor = g.Passed ? NeonPalette.Lime : NeonPalette.RedMagenta;
            string icon = g.Passed ? "+" : "x";
            string label = g.Passed
                ? $"  {icon} Gate {g.GateIndex + 1}: +{g.Score}  ({g.OffsetNormalized:0.00})"
                : $"  {icon} Gate {g.GateIndex + 1}: MISS  ({g.OffsetNormalized:0.00})";
            canvas.DrawString(label, cx, gateY + i * 17, HorizontalAlignment.Center);
        }

        // Lifetime stats line + seed
        float bottomY = rect.Height - 56;
        if (lifetime != null && lifetime.TotalSessions > 0)
        {
            canvas.FontSize = 9;
            canvas.FontColor = NeonPalette.TextMuted;
            canvas.DrawString(
                $"Session #{lifetime.TotalSessions}  |  Overall {lifetime.OverallAccuracy:0}%  |  {lifetime.UniqueSeedsPlayed} seeds",
                cx, bottomY, HorizontalAlignment.Center);
            bottomY += 14;
        }

        // Seed
        canvas.FontSize = 10;
        canvas.FontColor = NeonPalette.TextMuted;
        canvas.DrawString($"Seed: 0x{r.Seed:X8}", cx, bottomY, HorizontalAlignment.Center);

        // Pulsing footer
        float footerPulse = 0.3f + 0.5f * MathF.Abs(MathF.Sin(
            (float)Environment.TickCount64 * 0.002f));
        canvas.FontSize = 13;
        canvas.FontColor = NeonPalette.TextDim.WithAlpha(footerPulse);
        canvas.DrawString("PRESS RETRY OR NEW SEED",
            cx, rect.Height - 28, HorizontalAlignment.Center);
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 1 — Background
    // ══════════════════════════════════════════════════════

    private static void DrawBackground(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        var gradient = new LinearGradientPaint(
            new Point(0, 0), new Point(0, 1),
            new PaintGradientStop[]
            {
                new(0f, NeonPalette.BgDeep),
                new(1f, NeonPalette.BgMid)
            });
        canvas.SetFillPaint(gradient, new RectF(ox, oy, cw, ch));
        canvas.FillRectangle(ox, oy, cw, ch);
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 2 — Scanlines
    // ══════════════════════════════════════════════════════

    private static void DrawScanlines(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Colors.Black.WithAlpha(0.06f);

        for (float y = oy; y < oy + ch; y += 4f)
            canvas.DrawLine(ox, y, ox + cw, y);
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 3 — Corridor bounds
    // ══════════════════════════════════════════════════════

    private void DrawCorridorBounds(ICanvas canvas, float ox, float oy, float cw, float ch)
    {
        float pulse = 0.15f + 0.15f * MathF.Sin(_s.SimTime * 1.5f);

        // Sharp edge
        canvas.StrokeSize = 2;
        canvas.StrokeColor = NeonPalette.Cyan.WithAlpha(pulse);
        canvas.DrawLine(ox, oy, ox + cw, oy);
        canvas.DrawLine(ox, oy + ch, ox + cw, oy + ch);

        // Outer glow
        canvas.StrokeSize = 6;
        canvas.StrokeColor = NeonPalette.CyanGlow.WithAlpha(pulse * 0.4f);
        canvas.DrawLine(ox, oy, ox + cw, oy);
        canvas.DrawLine(ox, oy + ch, ox + cw, oy + ch);
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 4 — Parallax grids
    // ══════════════════════════════════════════════════════

    private void DrawParallaxLayers(ICanvas canvas, float ox, float oy,
        float cw, float ch, float scale)
    {
        DrawGridLayer(canvas, ox, oy, cw, ch, scale,
            scrollFactor: 0.3f, spacing: 120f,
            color: NeonPalette.TextMuted.WithAlpha(0.06f), strokeWidth: 0.5f);

        DrawGridLayer(canvas, ox, oy, cw, ch, scale,
            scrollFactor: 0.6f, spacing: 80f,
            color: NeonPalette.TextMuted.WithAlpha(0.04f), strokeWidth: 0.5f);
    }

    private void DrawGridLayer(ICanvas canvas, float ox, float oy,
        float cw, float ch, float scale,
        float scrollFactor, float spacing, Color color, float strokeWidth)
    {
        canvas.StrokeSize = strokeWidth;
        canvas.StrokeColor = color;

        float spacingScaled = spacing * scale;
        if (spacingScaled < 2f) return; // too dense to see

        float scrollOffset = (_s.ScrollPosition * scrollFactor * scale) % spacingScaled;

        // Vertical lines (scroll with parallax)
        for (float x = ox - scrollOffset; x <= ox + cw; x += spacingScaled)
        {
            if (x >= ox)
                canvas.DrawLine(x, oy, x, oy + ch);
        }

        // Horizontal lines (static)
        for (float y = oy; y <= oy + ch; y += spacingScaled)
            canvas.DrawLine(ox, y, ox + cw, y);
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 5 — Gates
    // ══════════════════════════════════════════════════════

    private void DrawGates(ICanvas canvas, float ox, float oy,
        float cw, float ch, float scale)
    {
        foreach (var gate in _s.Gates)
        {
            float screenX = ox + (gate.WallX - _s.ScrollPosition) * scale;

            // Cull off-screen
            if (screenX < ox - 40 || screenX > ox + cw + 40) continue;

            float gapHalf = gate.ApertureHeight * 0.5f * scale;
            float centerScreenY = oy + gate.CenterY * scale;

            Color gateColor;
            float wallAlpha;

            switch (gate.Status)
            {
                case GateVisualStatus.Passed:
                    gateColor = NeonPalette.LimeDim;
                    wallAlpha = 0.2f;
                    break;
                case GateVisualStatus.Missed:
                    gateColor = NeonPalette.RedDim;
                    wallAlpha = 0.25f;
                    break;
                default: // Upcoming
                    gateColor = NeonPalette.GateDifficultyColor(gate.Difficulty);
                    wallAlpha = 0.7f;
                    break;
            }

            // Wall lines
            canvas.StrokeSize = 2;
            canvas.StrokeColor = gateColor.WithAlpha(wallAlpha);
            canvas.DrawLine(screenX, oy, screenX, centerScreenY - gapHalf);
            canvas.DrawLine(screenX, centerScreenY + gapHalf, screenX, oy + ch);

            // Aperture edge glow (upcoming only)
            if (gate.Status == GateVisualStatus.Upcoming)
            {
                // Sharp aperture ticks
                canvas.StrokeSize = 3;
                canvas.StrokeColor = gateColor;
                canvas.DrawLine(screenX - 10, centerScreenY - gapHalf,
                               screenX + 10, centerScreenY - gapHalf);
                canvas.DrawLine(screenX - 10, centerScreenY + gapHalf,
                               screenX + 10, centerScreenY + gapHalf);

                // Soft glow halo
                canvas.StrokeSize = 8;
                canvas.StrokeColor = gateColor.WithAlpha(0.12f);
                canvas.DrawLine(screenX - 6, centerScreenY - gapHalf,
                               screenX + 6, centerScreenY - gapHalf);
                canvas.DrawLine(screenX - 6, centerScreenY + gapHalf,
                               screenX + 6, centerScreenY + gapHalf);

                // Gate index label (dim)
                canvas.FontSize = 9;
                canvas.FontColor = gateColor.WithAlpha(0.5f);
                canvas.DrawString($"{gate.GateIndex + 1}",
                    screenX + 4, oy + 14, HorizontalAlignment.Left);
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 6 — Gate flashes
    // ══════════════════════════════════════════════════════

    private void DrawGateFlashes(ICanvas canvas, float ox, float oy, float scale)
    {
        foreach (var flash in _s.ActiveFlashes)
        {
            float t = 1f - flash.Remaining / 0.2f; // 0..1 over 200ms
            if (t < 0f || t > 1f) continue;

            float radius = (20f + t * 60f) * scale;
            float alpha = (1f - t) * 0.6f;

            canvas.StrokeSize = 2;
            canvas.StrokeColor = flash.Color.WithAlpha(alpha);
            canvas.DrawCircle(
                ox + flash.CenterX * scale,
                oy + flash.CenterY * scale,
                radius);
        }
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 8 — Cursor trail
    // ══════════════════════════════════════════════════════

    private void DrawCursorTrail(ICanvas canvas, float ox, float oy, float scale)
    {
        var trail = _s.Trail;
        if (trail == null || trail.Count < 2) return;

        float currentTime = _s.SimTime;
        const float maxAge = 0.3f;

        for (int i = 1; i < trail.Count; i++)
        {
            var prev = trail.GetByAge(i - 1);
            var curr = trail.GetByAge(i);

            float age = currentTime - curr.Time;
            if (age > maxAge || age < 0f) continue;

            float ageFactor = 1f - (age / maxAge);

            // Thickness: newest=3, oldest=0.5
            float thickness = 0.5f + ageFactor * 2.5f;

            // Speed-based brightness boost
            float dx = curr.X - prev.X;
            float dy = curr.Y - prev.Y;
            float speed = MathF.Sqrt(dx * dx + dy * dy);
            float speedBoost = MathF.Min(speed * 0.002f, 0.3f);

            float alpha = ageFactor * 0.6f + speedBoost;

            canvas.StrokeSize = thickness;
            canvas.StrokeColor = NeonPalette.Cyan.WithAlpha(alpha);
            canvas.DrawLine(
                ox + prev.X * scale, oy + prev.Y * scale,
                ox + curr.X * scale, oy + curr.Y * scale);
        }
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 9 — Cursor + combo aura
    // ══════════════════════════════════════════════════════

    private void DrawCursor(ICanvas canvas, float ox, float oy, float scale)
    {
        float cx = ox + _s.CursorX * scale;
        float cy = oy + _s.CursorY * scale;

        // Combo aura (behind cursor)
        if (_s.Combo > 0)
        {
            float comboRadius = MathF.Min(18f + _s.Combo * 3f, 50f);
            float pulse = 0.3f + 0.2f * MathF.Sin(_s.SimTime * 6f);

            Color auraColor = _s.Combo >= 6 ? NeonPalette.Amber
                            : _s.Combo >= 3 ? NeonPalette.Lime
                            : NeonPalette.Cyan;

            // Outer ring
            canvas.StrokeSize = 2;
            canvas.StrokeColor = auraColor.WithAlpha(pulse * 0.4f);
            canvas.DrawCircle(cx, cy, comboRadius);

            // Inner soft fill
            canvas.FillColor = auraColor.WithAlpha(pulse * 0.08f);
            canvas.FillCircle(cx, cy, comboRadius * 0.7f);
        }

        // Outer glow
        canvas.FillColor = NeonPalette.CyanGlow;
        canvas.FillCircle(cx, cy, 14);

        // Mid glow
        canvas.FillColor = NeonPalette.Cyan.WithAlpha(0.3f);
        canvas.FillCircle(cx, cy, 8);

        // Core dot
        canvas.FillColor = _s.PrimaryDown ? NeonPalette.Lime : NeonPalette.Cyan;
        canvas.FillCircle(cx, cy, 4);
    }

    // ══════════════════════════════════════════════════════
    //  Playing: Layer 10 — HUD (never shaken)
    // ══════════════════════════════════════════════════════

    private void DrawHud(ICanvas canvas, float ox, float oy,
        float cw, float ch, float scale)
    {
        float hudY = oy + ch - 28;

        // Score (left)
        canvas.FontSize = 14;
        canvas.FontColor = NeonPalette.TextPrimary;
        canvas.DrawString($"{_s.Score}", ox + 16, hudY, HorizontalAlignment.Left);

        // Combo (center, only if > 0)
        if (_s.Combo > 0)
        {
            Color comboColor = _s.Combo >= 6 ? NeonPalette.Amber
                             : _s.Combo >= 3 ? NeonPalette.Lime
                             : NeonPalette.Cyan;
            canvas.FontSize = 16;
            canvas.FontColor = comboColor;
            canvas.DrawString($"x{_s.Combo}", ox + cw * 0.5f, hudY,
                HorizontalAlignment.Center);
        }

        // Gate progress (right)
        int crossedCount = 0;
        foreach (var g in _s.Gates)
        {
            if (g.Status != GateVisualStatus.Upcoming)
                crossedCount = g.GateIndex + 1;
        }

        canvas.FontSize = 12;
        canvas.FontColor = NeonPalette.TextDim;
        canvas.DrawString($"Gate {crossedCount}/{_s.GateCount}",
            ox + cw - 16, hudY, HorizontalAlignment.Right);

        // Progress bar (thin line at top)
        if (_s.GateCount > 0)
        {
            float progress = (float)crossedCount / _s.GateCount;
            float barW = cw * progress;
            canvas.FillColor = NeonPalette.Cyan.WithAlpha(0.4f);
            canvas.FillRectangle(ox, oy, barW, 2);
        }
    }
}
