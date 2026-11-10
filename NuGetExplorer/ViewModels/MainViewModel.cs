using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.IO.Compression;
using System.IO;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetExplorer.Infrastructure;
using NuGetExplorer.Models;
using NuGetExplorer.Services;
using SharpCompress.Archives;

namespace NuGetExplorer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly NuGetFeedService _feed;
    private readonly InstalledPackageService _installed;
    private string _projectPath = string.Empty, _searchText = string.Empty, _statusMessage = "Ready";
    private bool _isBusy;
    private PackageItemViewModel? _selectedPackage;

    public ObservableCollection<PackageItemViewModel> Packages { get; } = [];
    public ObservableCollection<PackageVersionViewModel> AvailableVersions { get; } = [];
    public ObservableCollection<PackageContentNode> PackageContent { get; } = [];
    public ICollectionView FilteredPackages { get; }
    public AsyncRelayCommand LoadPackagesCommand { get; }
    public string ProjectPath { get => _projectPath; set => SetProperty(ref _projectPath, value); }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) FilteredPackages.Refresh(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public PackageItemViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set { if (SetProperty(ref _selectedPackage, value)) _ = LoadPackageVersionsAsync(value); }
    }

    public MainViewModel(NuGetFeedService feed, InstalledPackageService installed)
    {
        _feed = feed;
        _installed = installed;
        FilteredPackages = CollectionViewSource.GetDefaultView(Packages);
        FilteredPackages.Filter = item => item is PackageItemViewModel package &&
            (string.IsNullOrWhiteSpace(SearchText) || package.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || package.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        LoadPackagesCommand = new AsyncRelayCommand(LoadPackagesAsync);
    }

    private async Task LoadPackagesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading packages from feed…";
            Packages.Clear(); AvailableVersions.Clear();
            PackageContent.Clear();
            var installed = _installed.GetInstalledPackages(ProjectPath);
            var metadata = await _feed.SearchPackagesAsync(string.Empty);
            foreach (var item in metadata)
            {
                installed.TryGetValue(item.Identity.Id, out var installedVersion);
                Packages.Add(new PackageItemViewModel(new PackageItem
                {
                    Id = item.Identity.Id,
                    Title = item.Title ?? item.Identity.Id,
                    Description = item.Description ?? string.Empty,
                    LatestVersion = item.Identity.Version.ToNormalizedString(),
                    InstalledVersion = installedVersion ?? string.Empty
                }));
            }
            StatusMessage = $"Loaded {Packages.Count} packages.";
        }
        catch (Exception ex) { StatusMessage = $"Could not load packages: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task LoadPackageVersionsAsync(PackageItemViewModel? package)
    {
        AvailableVersions.Clear();
        PackageContent.Clear();
        if (package is null) return;
        try
        {
            IsBusy = true;
            var versions = await _feed.GetPackageVersionsAsync(package.Id);
            var latest = versions.OrderByDescending(v => v, VersionComparer.VersionRelease).FirstOrDefault();
            foreach (var version in versions.OrderByDescending(v => v, VersionComparer.VersionRelease))
            {
                var text = version.ToNormalizedString();
                AvailableVersions.Add(new PackageVersionViewModel
                {
                    Version = text,
                    IsInstalled = NuGetVersion.TryParse(package.InstalledVersion, out var installed) && version.Equals(installed),
                    IsLatest = version.Equals(latest),
                    DownloadCommand = new AsyncRelayCommand(_ => DownloadPackageAsync(package, version))
                });
            }
            StatusMessage = $"Loaded {versions.Count} versions for {package.Id}.";
        }
        catch (Exception ex) { StatusMessage = $"Could not load versions: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DownloadPackageAsync(PackageItemViewModel package, NuGetVersion version)
    {
        try
        {
            IsBusy = true;
            var cacheDirectory = _feed.GetPackageCacheDirectory();
            Directory.CreateDirectory(cacheDirectory);
            var packagePath = Path.Combine(cacheDirectory, $"{package.Id}.{version.ToNormalizedString()}.nupkg");
            if (!File.Exists(packagePath))
            {
                StatusMessage = $"Downloading {package.Id} {version}…";
                await _feed.DownloadPackageAsync(package.Id, version, packagePath);
            }
            else
            {
                StatusMessage = $"Opening cached {package.Id} {version}…";
            }
            if (SelectedPackage?.Id == package.Id) LoadPackageTree(packagePath);
            StatusMessage = $"Showing contents of {package.Id} {version}.";
        }
        catch (Exception ex) { StatusMessage = $"Download failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void LoadPackageTree(string packagePath)
    {
        PackageContent.Clear();
        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries.Where(e => !string.IsNullOrEmpty(e.FullName)))
        {
            var node = AddPath(PackageContent, entry.FullName, entry.FullName.EndsWith('/'));
            if (!entry.FullName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) continue;

            node.IsArchive = true;
            try
            {
                using var embeddedStream = entry.Open();
                using var archiveBytes = new MemoryStream();
                embeddedStream.CopyTo(archiveBytes);
                archiveBytes.Position = 0;
                using var sevenZip = ArchiveFactory.OpenArchive(archiveBytes);
                foreach (var innerEntry in sevenZip.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
                {
                    AddPath(node.Children, innerEntry.Key!, innerEntry.IsDirectory);
                }
            }
            catch (Exception ex)
            {
                node.Children.Add(new PackageContentNode { Name = $"Unable to read archive: {ex.Message}" });
            }
        }
    }

    private static PackageContentNode AddPath(
        ObservableCollection<PackageContentNode> roots, string path, bool isDirectory)
    {
        var segments = path.TrimEnd('/', '\\').Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var current = roots;
        PackageContentNode? result = null;
        for (var index = 0; index < segments.Length; index++)
        {
            var isCurrentDirectory = index < segments.Length - 1 || isDirectory;
            var name = segments[index];
            result = current.FirstOrDefault(n => n.Name == name && n.IsDirectory == isCurrentDirectory);
            if (result is null)
            {
                result = new PackageContentNode { Name = name, IsDirectory = isCurrentDirectory };
                current.Add(result);
            }
            current = result.Children;
        }
        return result ?? new PackageContentNode();
    }
}
