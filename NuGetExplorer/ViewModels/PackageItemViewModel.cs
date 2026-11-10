using NuGetExplorer.Models;
using NuGetExplorer.Infrastructure;

namespace NuGetExplorer.ViewModels;

public sealed class PackageItemViewModel(PackageItem package)
{
    public string Id => package.Id;
    public string Title => package.Title;
    public string Description => package.Description;
    public string LatestVersion => package.LatestVersion;
    public string InstalledVersion => package.InstalledVersion;
    public bool IsInstalled => package.IsInstalled;
    public bool UpdateAvailable => package.UpdateAvailable;
    public string Status => !IsInstalled ? "Not installed" : UpdateAvailable ? "Update available" : "Up to date";
}

public sealed class PackageVersionViewModel
{
    public string Version { get; init; } = string.Empty;
    public bool IsInstalled { get; init; }
    public bool IsLatest { get; init; }
    public AsyncRelayCommand DownloadCommand { get; init; } = null!;
    public string Status => IsInstalled ? "Installed" : IsLatest ? "Latest" : "Available";
}

public sealed class PackageContentNode
{
    public string Name { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public bool IsArchive { get; set; }
    public string Icon => IsDirectory ? "📁" : IsArchive ? "🗜️" : "📄";
    public System.Collections.ObjectModel.ObservableCollection<PackageContentNode> Children { get; } = [];
}
