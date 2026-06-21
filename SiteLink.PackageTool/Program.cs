using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

return await PackageTool.RunAsync(args);

internal static class PackageTool
{
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


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "manifest" => BuildManifest(args.Skip(1).ToArray()),
                "index" => await BuildIndexAsync(args.Skip(1).ToArray()),
                _ => throw new ArgumentException($"Unknown mode '{args[0]}'.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int BuildManifest(string[] args)
    {
        Dictionary<string, string> options = ParseOptions(args);
        string projectPath = Required(options, "project");
        string outputPath = options.GetValueOrDefault("output") ?? "packageInfo.json";
        XDocument project = XDocument.Load(projectPath);

        string Property(string name) => project
            .Descendants(name)
            .Select(element => element.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        string repository = options.GetValueOrDefault("repository") ??
            Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? string.Empty;
        string[] repositoryParts = repository.Split('/', 2);

        PackageManifest manifest = new()
        {
            DisplayName = options.GetValueOrDefault("name") ??
                Property("Product") ?? Path.GetFileNameWithoutExtension(projectPath),
            Description = options.GetValueOrDefault("description") ??
                Property("Description") ?? string.Empty,
            Author = options.GetValueOrDefault("author") ??
                Property("Authors") ?? string.Empty,
            OwnerName = repositoryParts.ElementAtOrDefault(0) ?? string.Empty,
            RepositoryName = repositoryParts.ElementAtOrDefault(1) ?? string.Empty,
            PackageType = options.GetValueOrDefault("type") ?? "plugin",
            Version = options.GetValueOrDefault("version") ??
                Property("Version") ?? Property("AssemblyVersion") ?? "1.0.0",
            ApiVersion = options.GetValueOrDefault("api-version"),
            GameVersion = options.GetValueOrDefault("game-version")
        };

        foreach (string assetOption in args.Where(arg => arg.StartsWith("--asset=", StringComparison.OrdinalIgnoreCase)))
        {
            string value = assetOption["--asset=".Length..];
            string[] parts = value.Split('=', 2);
            if (parts.Length != 2)
                throw new ArgumentException("Assets use --asset=platform=path.");

            string platform = parts[0].ToLowerInvariant();
            string file = Path.GetFullPath(parts[1]);
            manifest.Platforms[platform] = new PackageAsset
            {
                Platform = platform,
                FileName = Path.GetFileName(file),
                FileUrl = options.GetValueOrDefault($"url-{platform}"),
                Sha256 = ComputeSha256(file),
                Size = new FileInfo(file).Length
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine($"Generated {outputPath}");
        return 0;
    }

    private static async Task<int> BuildIndexAsync(string[] args)
    {
        Dictionary<string, string> options = ParseOptions(args);
        string repository = Required(options, "repository");
        string outputPath = options.GetValueOrDefault("output") ?? "Website/releases.json";
        string token = options.GetValueOrDefault("token") ??
            Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        using HttpClient http = new();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SiteLink-PackageTool/1.0");
        if (!string.IsNullOrWhiteSpace(token))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        string releasesJson = await http.GetStringAsync(
            $"https://api.github.com/repos/{repository}/releases");
        using JsonDocument releases = JsonDocument.Parse(releasesJson);
        PackageReleaseIndex index = null;

        foreach (JsonElement release in releases.RootElement.EnumerateArray())
        {
            JsonElement manifestAsset = release.GetProperty("assets")
                .EnumerateArray()
                .FirstOrDefault(asset =>
                    asset.GetProperty("name").GetString()
                        ?.Equals("packageInfo.json", StringComparison.OrdinalIgnoreCase) == true);

            if (manifestAsset.ValueKind == JsonValueKind.Undefined)
                continue;

            string manifestUrl = manifestAsset.GetProperty("browser_download_url").GetString()!;
            PackageManifest manifest = JsonSerializer.Deserialize<PackageManifest>(
                await http.GetStringAsync(manifestUrl), JsonOptions);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
                continue;

            foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
            {
                string assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                foreach (PackageAsset packageAsset in manifest.Platforms.Values
                             .Where(value => value.FileName.Equals(assetName, StringComparison.OrdinalIgnoreCase)))
                {
                    packageAsset.FileUrl = asset.GetProperty("browser_download_url").GetString();
                    packageAsset.Size = asset.GetProperty("size").GetInt64();
                }
            }

            index ??= new PackageReleaseIndex
            {
                DisplayName = manifest.DisplayName,
                Description = manifest.Description,
                Author = manifest.Author,
                OwnerName = manifest.OwnerName,
                RepositoryName = manifest.RepositoryName,
                PackageType = manifest.PackageType
            };
            index.Versions[manifest.Version] = manifest;
        }

        if (index == null)
            throw new InvalidOperationException("No releases containing packageInfo.json were found.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(index, JsonOptions));
        Console.WriteLine($"Generated {outputPath}");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args) =>
        args.Where(arg => arg.StartsWith("--") && arg.Contains('='))
            .Select(arg => arg[2..].Split('=', 2))
            .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.OrdinalIgnoreCase);

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing --{key}=...");

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("manifest --project=project.csproj --repository=owner/name --asset=windows=path [--asset=linux=path]");
        Console.WriteLine("index --repository=owner/name [--token=token] [--output=Website/releases.json]");
        Console.WriteLine("translations [--output=Translations/language_en.json]");
    }
}
