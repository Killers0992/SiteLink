namespace SiteLink.API.Translations;

public static class TranslationManager
{
    public const string TranslationsDirectory = "Translations";
    public const string DefaultLanguage = "en";

    public static LanguageTranslations Current { get; private set; } = new();
    public static string CurrentLanguage { get; private set; } = DefaultLanguage;

    public static void Load(string language)
    {
        Directory.CreateDirectory(TranslationsDirectory);

        string defaultPath = GetLanguagePath(DefaultLanguage);
        EnsureLanguageFile(defaultPath);

        string normalizedLanguage = NormalizeLanguage(language);
        string selectedPath = GetLanguagePath(normalizedLanguage);

        if (!File.Exists(selectedPath))
        {
            SiteLinkLogger.Info(
                $"Translation file '{selectedPath}' was not found. Falling back to '{DefaultLanguage}'.",
                "Translations");

            normalizedLanguage = DefaultLanguage;
            selectedPath = defaultPath;
        }

        try
        {
            Current = YamlParser.Deserializer.Deserialize<LanguageTranslations>(
                File.ReadAllText(selectedPath)) ?? new LanguageTranslations();
            CurrentLanguage = normalizedLanguage;
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error($"Failed to load translations from '{selectedPath}': {ex}", "Translations");
            Current = new LanguageTranslations();
            CurrentLanguage = DefaultLanguage;
        }
    }

    public static PlaceholderFormatter Format(string template) => new(template);

    private static void EnsureLanguageFile(string path)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, YamlParser.Serializer.Serialize(new LanguageTranslations()));
    }

    private static string GetLanguagePath(string language) =>
        Path.Combine(TranslationsDirectory, $"language_{language}.yml");

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return DefaultLanguage;

        string normalized = new(language
            .Trim()
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());

        return string.IsNullOrEmpty(normalized) ? DefaultLanguage : normalized;
    }
}
