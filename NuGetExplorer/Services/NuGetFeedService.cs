using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Credentials;
using NuGet.Packaging.Core;
using System.IO;

namespace NuGetExplorer.Services;

public sealed class NuGetFeedService
{
    private readonly SourceRepository _repository;

    public NuGetFeedService(string feedUrl)
    {
        DefaultCredentialServiceUtility.SetupDefaultCredentialService(
            NullLogger.Instance,
            nonInteractive: false);

        var packageSource = new PackageSource(feedUrl);

        _repository = Repository.Factory.GetCoreV3(packageSource);
    }

    public async Task<IReadOnlyList<IPackageSearchMetadata>> SearchPackagesAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        var resource = await _repository.GetResourceAsync<PackageSearchResource>();
        var filter = new SearchFilter(includePrerelease: false);
        var packages = new List<IPackageSearchMetadata>();
        const int pageSize = 100;

        for (var skip = 0; ; skip += pageSize)
        {
            var results = await resource.SearchAsync(
                searchText ?? string.Empty,
                filter,
                skip,
                pageSize,
                NullLogger.Instance,
                cancellationToken);

            var page = results.ToList();
            packages.AddRange(page);

            if (page.Count < pageSize)
                break;
        }

        return packages;
    }

    public async Task<IReadOnlyList<NuGetVersion>> GetPackageVersionsAsync(
    string packageId,
    CancellationToken cancellationToken = default)
    {
        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>();

        using var cacheContext = new SourceCacheContext();

        var versions = await resource.GetAllVersionsAsync(
            packageId,
            cacheContext,
            NullLogger.Instance,
            cancellationToken);

        return versions
            .OrderByDescending(x => x, VersionComparer.VersionRelease)
            .ToList();
    }

    public async Task DownloadPackageAsync(string packageId, NuGetVersion version, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var resource = await _repository.GetResourceAsync<DownloadResource>();
        using var cacheContext = new SourceCacheContext();
        var context = new PackageDownloadContext(cacheContext);
        var identity = new PackageIdentity(packageId, version);
        var result = await resource.GetDownloadResourceResultAsync(identity, context,
            Path.GetTempPath(), NullLogger.Instance, cancellationToken);
        if (result.Status != DownloadResourceResultStatus.Available || result.PackageStream is null)
            throw new InvalidOperationException($"The feed did not return {packageId} {version}.");

        var temporaryPath = destinationPath + ".partial";
        try
        {
            using (result.PackageStream)
            await using (var output = File.Create(temporaryPath))
                await result.PackageStream.CopyToAsync(output, cancellationToken);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public string GetPackageCacheDirectory() => Path.Combine(AppContext.BaseDirectory, "NugetFiles");
}
