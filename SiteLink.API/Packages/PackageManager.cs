using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SiteLink.API.Packages;

public static class PackageManager
{
    private static readonly HttpClient Http = CreateHttpClient();

    public static string Platform =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos" :
        "any";

    public static async Task<PackageReleaseIndex> GetIndexAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        (string owner, string name) = ParseRepository(repository);

        try
        {
            string url = $"https://{owner.ToLowerInvariant()}.github.io/{name}/releases.json";
            using HttpResponseMessage response = await Http.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                PackageReleaseIndex index = JsonConvert.DeserializeObject<PackageReleaseIndex>(json);
                if (index != null && index.Versions != null && index.Versions.Count > 0)
                    return index;
            }
        }
        catch
        {
            // Fall back to GitHub REST API if releases.json is missing or invalid
        }

        return await GetIndexFromGitHubApiAsync(owner, name, cancellationToken);
    }

    public static PackageManifest GetLatest(
        PackageReleaseIndex index,
        Version installedVersion = null)
    {
        return index?.Versions?
            .Select(pair => (Manifest: pair.Value, Parsed: ParseVersion(pair.Key)))
            .Where(item => item.Parsed != null &&
                (installedVersion == null || item.Parsed > installedVersion))
            .OrderByDescending(item => item.Parsed)
            .Select(item => item.Manifest)
            .FirstOrDefault();
    }

    public static PackageAsset GetAsset(PackageManifest manifest)
    {
        if (manifest == null)
            return null;

        if (manifest.Platforms.TryGetValue(Platform, out PackageAsset platform))
            return platform;

        return manifest.Platforms.TryGetValue("any", out PackageAsset any) ? any : null;
    }

    public static async Task<string> DownloadAsync(
        PackageManifest manifest,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        PackageAsset asset = GetAsset(manifest) ??
            throw new InvalidOperationException(
                $"Version {manifest?.Version} has no asset for platform '{Platform}'.");

        if (string.IsNullOrWhiteSpace(asset.FileUrl))
            throw new InvalidDataException("The selected release asset has no download URL.");

        Directory.CreateDirectory(destinationDirectory);
        string destination = Path.Combine(destinationDirectory, asset.FileName);
        using HttpResponseMessage response = await Http.GetAsync(asset.FileUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        byte[] data = await response.Content.ReadAsByteArrayAsync();

        string hash;
        using (SHA256 sha256 = SHA256.Create())
            hash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(asset.Sha256) &&
            !hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Hash mismatch for {asset.FileName}. Expected {asset.Sha256}, received {hash}.");

        File.WriteAllBytes(destination, data);
        return destination;
    }

    public static async Task<string> InstallPluginAsync(
        string repository,
        CancellationToken cancellationToken = default)
    {
        PackageReleaseIndex index = await GetIndexAsync(repository, cancellationToken);
        PackageManifest latest = GetLatest(index) ??
            throw new InvalidOperationException($"No valid releases exist for {repository}.");
        string downloaded = await DownloadAsync(latest, "Updates", cancellationToken);
        string destination = Path.Combine(PluginsManager.PluginsPath, Path.GetFileName(downloaded));
        File.Copy(downloaded, destination, true);
        return destination;
    }

    public static async Task<PackageManifest> CheckPluginAsync(
        Plugin plugin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plugin?.Repository))
            return null;

        PackageReleaseIndex index = await GetIndexAsync(plugin.Repository, cancellationToken);
        return GetLatest(index, plugin.Version);
    }

    public static async Task<PackageManifest> CheckCoreAsync(
        Version installedVersion,
        CancellationToken cancellationToken = default)
    {
        PackageReleaseIndex index = await GetIndexAsync(
            SiteLinkSettings.Singleton.UpdateRepository,
            cancellationToken);
        return GetLatest(index, installedVersion);
    }

    public static async Task ApplyCoreUpdateAsync(
        PackageManifest manifest,
        CancellationToken cancellationToken = default)
    {
        string downloaded = await DownloadAsync(manifest, "Updates", cancellationToken);
        string executable = Process.GetCurrentProcess().MainModule?.FileName ??
            throw new InvalidOperationException("Could not determine the current executable path.");
        string workingDirectory = Environment.CurrentDirectory;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string script = Path.Combine("Updates", "apply-update.cmd");
            File.WriteAllText(script,
                $"@echo off\r\n" +
                $"ping 127.0.0.1 -n 3 > nul\r\n" +
                $"copy /Y \"{downloaded}\" \"{executable}\"\r\n" +
                $"start \"\" /D \"{workingDirectory}\" \"{executable}\"\r\n" +
                $"del \"%~f0\"\r\n");
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{Path.GetFullPath(script)}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            });
        }
        else
        {
            string script = Path.Combine("Updates", "apply-update.sh");
            File.WriteAllText(script,
                "#!/bin/sh\n" +
                "sleep 2\n" +
                $"cp \"{downloaded}\" \"{executable}\"\n" +
                $"chmod +x \"{executable}\"\n" +
                $"cd \"{workingDirectory}\"\n" +
                $"\"{executable}\" &\n" +
                "rm -- \"$0\"\n");
            Process.Start(new ProcessStartInfo("/bin/sh", Path.GetFullPath(script))
            {
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            });
        }

        Environment.Exit(0);
    }

    public static (string Owner, string Repository) ParseRepository(string repository)
    {
        string[] parts = repository?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts?.Length != 2)
            throw new ArgumentException("Repository must use owner/name format.", nameof(repository));

        return (parts[0], parts[1]);
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value?.TrimStart('v', 'V'), out Version version) ? version : null;

    private static async Task<PackageReleaseIndex> GetIndexFromGitHubApiAsync(
        string owner,
        string name,
        CancellationToken cancellationToken)
    {
        string url = $"https://api.github.com/repos/{owner}/{name}/releases";
        using HttpResponseMessage response = await Http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        JArray releases = JArray.Parse(json);

        PackageReleaseIndex index = new()
        {
            DisplayName = name,
            OwnerName = owner,
            RepositoryName = name
        };

        foreach (JObject release in releases.Children<JObject>())
        {
            if (release["draft"]?.Value<bool>() == true)
                continue;

            string tagName = release["tag_name"]?.ToString();
            if (string.IsNullOrWhiteSpace(tagName))
                continue;

            string versionStr = tagName.TrimStart('v', 'V');
            PackageManifest manifest = new()
            {
                DisplayName = name,
                OwnerName = owner,
                RepositoryName = name,
                Version = versionStr,
                PackageType = "core"
            };

            if (release["assets"] is JArray assets)
            {
                foreach (JObject asset in assets.Children<JObject>())
                {
                    string fileName = asset["name"]?.ToString();
                    string downloadUrl = asset["browser_download_url"]?.ToString();
                    long size = asset["size"]?.Value<long>() ?? 0;

                    if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(downloadUrl))
                        continue;

                    string platform;
                    if (fileName.Equals("SiteLink.exe", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains("win", StringComparison.OrdinalIgnoreCase))
                    {
                        platform = "windows";
                    }
                    else if (fileName.Equals("SiteLink", StringComparison.OrdinalIgnoreCase) ||
                             fileName.Contains("linux", StringComparison.OrdinalIgnoreCase))
                    {
                        platform = "linux";
                    }
                    else if (fileName.Contains("mac", StringComparison.OrdinalIgnoreCase) ||
                             fileName.Contains("osx", StringComparison.OrdinalIgnoreCase))
                    {
                        platform = "macos";
                    }
                    else
                    {
                        platform = "any";
                    }

                    manifest.Platforms[platform] = new PackageAsset
                    {
                        Platform = platform,
                        FileName = fileName,
                        FileUrl = downloadUrl,
                        Size = size
                    };
                }
            }

            if (index.DisplayName == name && release["name"] != null)
            {
                string relName = release["name"].ToString();
                if (!string.IsNullOrWhiteSpace(relName))
                    index.DisplayName = relName;
            }

            index.Versions[versionStr] = manifest;
        }

        if (index.Versions.Count == 0)
            throw new InvalidOperationException($"No valid releases found in GitHub repository '{owner}/{name}'.");

        return index;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SiteLink-Updater/1.0");
        return client;
    }
}
