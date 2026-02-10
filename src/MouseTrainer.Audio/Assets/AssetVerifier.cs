using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MouseTrainer.Audio.Assets;

/// <summary>
/// Verifies required assets are present and openable.
/// Call this at app startup to prevent silent missing-audio failures.
/// </summary>
public static class AssetVerifier
{
    public static async Task<IReadOnlyList<string>> VerifyRequiredAudioAsync(IAssetOpener opener, CancellationToken ct = default)
    {
        var missing = new List<string>();

        foreach (var name in AssetManifest.RequiredAudio)
        {
            try
            {
                await using var s = await opener.OpenAsync(name, ct);
                if (s is null) missing.Add(name);
            }
            catch
            {
                missing.Add(name);
            }
        }

        return missing;
    }
}
