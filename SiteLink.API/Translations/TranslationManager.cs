namespace SiteLink.API.Translations;

public static class TranslationManager
{
    public const string TranslationsDirectory = "Translations";
    public const string DefaultLanguage = "en";

    private static readonly List<ITranslationCatalog> Catalogs = new();
    private static TranslationCatalog<LanguageTranslations> _core;

    public static LanguageTranslations Current =>
        _core?.Get(CurrentLanguage) ?? new LanguageTranslations();
    public static string CurrentLanguage { get; private set; } = DefaultLanguage;
    public static IEnumerable<string> AvailableLanguages =>
        _core?.Languages ?? new[] { DefaultLanguage };

    public static void Load(string language)
    {
        CurrentLanguage = NormalizeLanguage(language);
        Directory.CreateDirectory(TranslationsDirectory);

        _core?.Dispose();
        _core = new TranslationCatalog<LanguageTranslations>(
            "SiteLink",
            TranslationsDirectory,
            DefaultLanguage);

        lock (Catalogs)
        {
            Catalogs.RemoveAll(catalog =>
                catalog.Owner.Equals("SiteLink", StringComparison.OrdinalIgnoreCase));
            Catalogs.Add(_core);
        }

        PlayerLanguageStore.Load();
    }

    public static LanguageTranslations For(Session session) =>
        _core?.Get(GetLanguage(session)) ?? Current;

    public static string GetLanguage(Session session)
    {
        string preferred = PlayerLanguageStore.Get(session?.UserId);
        if (!string.IsNullOrWhiteSpace(preferred) &&
            AvailableLanguages.Contains(preferred, StringComparer.OrdinalIgnoreCase))
            return NormalizeLanguage(preferred);

        return CurrentLanguage;
    }

    public static bool SetPlayerLanguage(string userId, string language)
    {
        string normalized = NormalizeLanguage(language);
        if (!AvailableLanguages.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return false;

        PlayerLanguageStore.Set(userId, normalized);
        return true;
    }

    public static PlaceholderFormatter Format(string template, TranslationContext context = null) =>
        new(template, context);

    public static string Command(string key, TranslationContext context = null)
    {
        if (!Current.Commands.TryGetValue(key, out string template))
            return key;

        return Format(template, context).Format();
    }

    public static string Log(string key, TranslationContext context = null)
    {
        if (!Current.Logs.TryGetValue(key, out string template))
            return key;

        return Format(template, context).Format();
    }

    public static string FormatFor(
        Session session,
        Func<LanguageTranslations, string> selector,
        TranslationContext context = null)
    {
        LanguageTranslations translations = For(session);
        context ??= TranslationContext.For(session);
        return Format(selector(translations), context).Format();
    }

    public static TranslationCatalog<TTranslation> RegisterCatalog<TTranslation>(
        string owner,
        string directory,
        string defaultLanguage = DefaultLanguage)
        where TTranslation : class, new()
    {
        TranslationCatalog<TTranslation> catalog =
            new(owner, directory, defaultLanguage);

        lock (Catalogs)
            Catalogs.Add(catalog);

        return catalog;
    }

    public static void UnregisterCatalog(ITranslationCatalog catalog)
    {
        if (catalog == null)
            return;

        lock (Catalogs)
            Catalogs.Remove(catalog);

        catalog.Dispose();
    }

    public static void ReloadAll()
    {
        lock (Catalogs)
        {
            foreach (ITranslationCatalog catalog in Catalogs.ToArray())
                catalog.Reload();
        }
    }

    public static IReadOnlyList<TranslationValidationResult> ValidateAll()
    {
        lock (Catalogs)
            return Catalogs.SelectMany(catalog => catalog.Validate()).ToArray();
    }

    public static string NormalizeLanguage(string language)
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
