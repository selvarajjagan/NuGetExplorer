using NuGet.Versioning;

namespace NuGetExplorer.Models;

public sealed class PackageItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string InstalledVersion { get; init; } = string.Empty;

    public bool IsInstalled => !string.IsNullOrWhiteSpace(InstalledVersion);

    public bool UpdateAvailable => IsInstalled &&
        NuGetVersion.TryParse(LatestVersion, out var latest) &&
        NuGetVersion.TryParse(InstalledVersion, out var installed) &&
        latest > installed;
}
