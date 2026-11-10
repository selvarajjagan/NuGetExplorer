using System.Xml.Linq;
using System.IO;

namespace NuGetExplorer.Services;

public sealed class InstalledPackageService
{
    public Dictionary<string, string> GetInstalledPackages(string projectPath)
    {
        var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            return packages;

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        var project = XDocument.Load(projectPath);
        var centralVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var propsPath = FindDirectoryPackagesProps(projectDirectory);
        if (propsPath is not null)
        {
            var props = XDocument.Load(propsPath);
            foreach (var entry in props.Descendants().Where(x => x.Name.LocalName == "PackageVersion"))
            {
                var id = entry.Attribute("Include")?.Value ?? entry.Attribute("Update")?.Value;
                var version = entry.Attribute("Version")?.Value ??
                              entry.Elements().FirstOrDefault(x => x.Name.LocalName == "Version")?.Value;
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version) && !version.StartsWith("$("))
                    centralVersions[id] = version;
            }
        }

        foreach (var entry in project.Descendants().Where(x => x.Name.LocalName == "PackageReference"))
        {
            var id = entry.Attribute("Include")?.Value ?? entry.Attribute("Update")?.Value;
            var version = entry.Attribute("Version")?.Value ??
                          entry.Elements().FirstOrDefault(x => x.Name.LocalName == "Version")?.Value;
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (string.IsNullOrWhiteSpace(version)) centralVersions.TryGetValue(id, out version);
            if (!string.IsNullOrWhiteSpace(version) && !version.StartsWith("$(")) packages[id] = version;
        }
        return packages;
    }

    private static string? FindDirectoryPackagesProps(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "Directory.Packages.props");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
