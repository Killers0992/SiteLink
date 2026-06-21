using SiteLink.API.Storage.Providers;

namespace SiteLink.API.Storage;

public static class StorageManager
{
    private static readonly ConcurrentDictionary<string, PlayerDataStore> Stores =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Func<StorageSettings, IStorageProvider>> Factories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["json"] = settings => new JsonStorageProvider(settings.JsonPath),
            ["sqlite"] = settings => new SqliteStorageProvider(settings.SqlitePath, settings.TableName),
            ["mysql"] = settings => new MySqlStorageProvider(
                settings.MysqlConnectionString,
                settings.TableName)
        };

    public static IStorageProvider Provider { get; private set; }
    public static PlayerDataStore Core => GetStore("sitelink");

    public static void Initialize(StorageSettings settings)
    {
        settings ??= new StorageSettings();
        string providerName = settings.Provider?.Trim() ?? "json";

        if (!Factories.TryGetValue(providerName, out Func<StorageSettings, IStorageProvider> factory))
            throw new InvalidOperationException(
                $"Unknown storage provider '{providerName}'. Available: {string.Join(", ", Factories.Keys)}.");

        Provider?.Dispose();
        Provider = factory(settings);
        Provider.InitializeAsync().GetAwaiter().GetResult();
        SiteLinkLogger.Info($"Storage provider '{Provider.Name}' initialized.", "Storage");
    }

    public static void RegisterProvider(
        string name,
        Func<StorageSettings, IStorageProvider> factory,
        bool replace = false)
    {
        string normalized = NormalizeSegment(name, nameof(name));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        lock (Factories)
        {
            if (!replace && Factories.ContainsKey(normalized))
                throw new InvalidOperationException($"Storage provider '{normalized}' is already registered.");

            Factories[normalized] = factory;
        }
    }

    public static PlayerDataStore GetStore(string scope) =>
        Stores.GetOrAdd(
            NormalizeSegment(scope, nameof(scope)),
            normalized => new PlayerDataStore(normalized));

    public static PlayerDataStore ForPlugin(Plugin plugin) =>
        plugin == null
            ? throw new ArgumentNullException(nameof(plugin))
            : GetStore($"plugin:{plugin.Name}");

    internal static string NormalizeSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty.", parameterName);

        return value.Trim();
    }
}
