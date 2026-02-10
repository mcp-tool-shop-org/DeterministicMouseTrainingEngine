using MouseTrainer.Audio.Assets;

namespace MouseTrainer.MauiHost;

/// <summary>
/// Opens packaged assets via MAUI's app package APIs.
/// This is the deterministic, MSIX-safe way to access bundled audio.
/// </summary>
public sealed class MauiAssetOpener : IAssetOpener
{
    public async Task<Stream> OpenAsync(string assetName, CancellationToken ct = default)
    {
        // NOTE: assetName must match the LogicalName used by MauiAsset.
        // We use Resources/Raw with LogicalName="%(RecursiveDir)%(Filename)%(Extension)" so root names resolve directly.
        var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
        return stream;
    }
}
