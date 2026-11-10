using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Credentials;

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
}