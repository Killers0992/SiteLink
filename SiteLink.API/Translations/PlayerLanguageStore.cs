namespace SiteLink.API.Translations;

public static class PlayerLanguageStore
{
    private const string FilePath = "Translations/player_languages.yml";
    private static readonly object Sync = new();
    private static Dictionary<string, string> _languages =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Load()
    {
        lock (Sync)
        {
            if (!File.Exists(FilePath))
            {
                Save();
                return;
            }

            try
            {
                _languages = YamlParser.Deserializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(FilePath)) ?? new(StringComparer.OrdinalIgnoreCase);
                _languages = new Dictionary<string, string>(_languages, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Failed to load player languages: {ex}", "Translations");
                _languages = new(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public static string Get(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        lock (Sync)
            return _languages.TryGetValue(userId, out string language) ? language : null;
    }

    public static void Set(string userId, string language)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        lock (Sync)
        {
            _languages[userId] = TranslationManager.NormalizeLanguage(language);
            Save();
        }
    }

    public static bool Remove(string userId)
    {
        lock (Sync)
        {
            bool removed = _languages.Remove(userId);
            if (removed)
                Save();

            return removed;
        }
    }

    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, YamlParser.Serializer.Serialize(_languages));
    }
}
