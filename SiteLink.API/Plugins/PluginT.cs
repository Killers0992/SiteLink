namespace SiteLink.API.Plugins;

public abstract class Plugin<T> : Plugin where T : class, new()
{
    string _configPath => Path.Combine(PluginDirectory, "config.yml");

    public T Config { get; private set; }

    public override void LoadConfig()
    {
        if (!Directory.Exists(PluginDirectory))
            Directory.CreateDirectory(PluginDirectory);

        if (!File.Exists(_configPath))
            File.WriteAllText(_configPath, YamlParser.Serializer.Serialize(Activator.CreateInstance(typeof(T))));

        string text = File.ReadAllText(_configPath);
        Config = YamlParser.Deserializer.Deserialize<T>(text);
        SaveConfig();
    }

    public override void SaveConfig()
    {
        if (!Directory.Exists(PluginDirectory))
            Directory.CreateDirectory(PluginDirectory);

        File.WriteAllText(_configPath, YamlParser.Serializer.Serialize(Config));
    }
}

/// <summary>
/// Plugin base with strongly typed configuration and per-language translations.
/// Files are stored under Plugins/{plugin}/Translations/language_{code}.json.
/// </summary>
public abstract class Plugin<TConfig, TTranslation> : Plugin<TConfig>
    where TConfig : class, new()
    where TTranslation : class, new()
{
    private TranslationCatalog<TTranslation> _translations;

    public override ITranslationCatalog TranslationCatalog => _translations;

    public TTranslation Translation =>
        _translations?.Get(TranslationManager.CurrentLanguage) ?? new TTranslation();

    public TTranslation GetTranslation(Session session) =>
        _translations?.Get(TranslationManager.GetLanguage(session)) ?? Translation;

    public override void LoadTranslations()
    {
        _translations?.Dispose();
        _translations = TranslationManager.RegisterCatalog<TTranslation>(
            Name,
            Path.Combine(PluginDirectory, "Translations"));
    }

    public string Translate(
        Session session,
        Func<TTranslation, string> selector,
        TranslationContext context = null)
    {
        context ??= TranslationContext.For(session, plugin: this);
        return TranslationManager.Format(selector(GetTranslation(session)), context).Format();
    }
}
