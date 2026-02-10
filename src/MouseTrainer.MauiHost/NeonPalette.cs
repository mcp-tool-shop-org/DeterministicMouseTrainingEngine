namespace MouseTrainer.MauiHost;

/// <summary>
/// Brand-neutral neon color constants. Single source of truth for all rendering.
/// </summary>
public static class NeonPalette
{
    // ── Background gradient ──────────────────────────────────
    public static readonly Color BgDeep  = Color.FromArgb("#0D0F14");
    public static readonly Color BgMid   = Color.FromArgb("#12151C");

    // ── Primary neon (cyan/teal) ─────────────────────────────
    public static readonly Color Cyan     = Color.FromArgb("#00E5FF");
    public static readonly Color CyanDim  = Color.FromArgb("#00E5FF").WithAlpha(0.4f);
    public static readonly Color CyanGlow = Color.FromArgb("#00E5FF").WithAlpha(0.15f);

    // ── Success (lime/green) ─────────────────────────────────
    public static readonly Color Lime    = Color.FromArgb("#76FF03");
    public static readonly Color LimeDim = Color.FromArgb("#76FF03").WithAlpha(0.5f);

    // ── Warning (amber) ──────────────────────────────────────
    public static readonly Color Amber    = Color.FromArgb("#FFD740");
    public static readonly Color AmberDim = Color.FromArgb("#FFD740").WithAlpha(0.5f);

    // ── Error (red-magenta) ──────────────────────────────────
    public static readonly Color RedMagenta = Color.FromArgb("#FF1744");
    public static readonly Color RedDim     = Color.FromArgb("#FF1744").WithAlpha(0.4f);

    // ── Ghost (amber, low alpha) ──────────────────────────
    public static readonly Color Ghost     = Amber.WithAlpha(0.25f);
    public static readonly Color GhostGlow = Amber.WithAlpha(0.10f);

    // ── Text ─────────────────────────────────────────────────
    public static readonly Color TextPrimary = Color.FromArgb("#E0E0E0");
    public static readonly Color TextDim     = Color.FromArgb("#666666");
    public static readonly Color TextMuted   = Color.FromArgb("#444444");

    // ── Corridor ─────────────────────────────────────────────
    public static readonly Color CorridorEdge = Color.FromArgb("#00E5FF").WithAlpha(0.25f);

    /// <summary>
    /// Gate difficulty color ramp: Lime (easy) → Amber (mid) → RedMagenta (hard).
    /// </summary>
    public static Color GateDifficultyColor(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        if (t < 0.5f)
        {
            float u = t * 2f;
            return Lerp(Lime, Amber, u);
        }
        else
        {
            float u = (t - 0.5f) * 2f;
            return Lerp(Amber, RedMagenta, u);
        }
    }

    /// <summary>
    /// Per-channel linear interpolation between two colors.
    /// </summary>
    public static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            a.Red + (b.Red - a.Red) * t,
            a.Green + (b.Green - a.Green) * t,
            a.Blue + (b.Blue - a.Blue) * t,
            a.Alpha + (b.Alpha - a.Alpha) * t);
    }
}
