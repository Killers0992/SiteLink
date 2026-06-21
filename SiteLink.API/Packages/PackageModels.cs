namespace SiteLink.API.Packages;

public sealed class PackageManifest
{
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string OwnerName { get; set; }
    public string RepositoryName { get; set; }
    public string PackageType { get; set; } = "plugin";
    public string Version { get; set; }
    public string ApiVersion { get; set; }
    public string GameVersion { get; set; }
    public Dictionary<string, PackageAsset> Platforms { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PackageAsset
{
    public string Platform { get; set; }
    public string FileName { get; set; }
    public string FileUrl { get; set; }
    public string Sha256 { get; set; }
    public long Size { get; set; }
}

public sealed class PackageReleaseIndex
{
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string OwnerName { get; set; }
    public string RepositoryName { get; set; }
    public string PackageType { get; set; }
    public Dictionary<string, PackageManifest> Versions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
