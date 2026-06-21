using System.Reflection;
using System.Text.RegularExpressions;

namespace SiteLink.API.Translations;

public interface ITranslationCatalog : IDisposable
{
    string Owner { get; }
    string DirectoryPath { get; }
    Type TranslationType { get; }
    IEnumerable<string> Languages { get; }
    void Reload();
    IReadOnlyList<TranslationValidationResult> Validate();
}

public sealed class TranslationCatalog<TTranslation> : ITranslationCatalog
    where TTranslation : class, new()
{
    private static readonly Regex PlaceholderRegex =
        new(@"\{([a-zA-Z0-9_.-]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly object _sync = new();
    private readonly Dictionary<string, TTranslation> _translations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher _watcher;
    private Timer _reloadTimer;

    public string Owner { get; }
    public string DirectoryPath { get; }
    public string DefaultLanguage { get; }
    public Type TranslationType => typeof(TTranslation);
    public IEnumerable<string> Languages
    {
        get
        {
            lock (_sync)
                return _translations.Keys.ToArray();
        }
    }

    public event Action Reloaded;

    public TranslationCatalog(string owner, string directoryPath, string defaultLanguage)
    {
        Owner = owner;
        DirectoryPath = directoryPath;
        DefaultLanguage = TranslationManager.NormalizeLanguage(defaultLanguage);

        Directory.CreateDirectory(DirectoryPath);
        EnsureDefaultFile();
        Reload();

        _watcher = new FileSystemWatcher(DirectoryPath, "language_*.yml")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnTranslationFileChanged;
        _watcher.Created += OnTranslationFileChanged;
        _watcher.Deleted += OnTranslationFileChanged;
        _watcher.Renamed += OnTranslationFileChanged;
    }

    public TTranslation Get(string language)
    {
        string normalized = TranslationManager.NormalizeLanguage(language);

        lock (_sync)
        {
            if (_translations.TryGetValue(normalized, out TTranslation selected))
                return selected;

            if (_translations.TryGetValue(DefaultLanguage, out TTranslation fallback))
                return fallback;

            return new TTranslation();
        }
    }

    public void Reload()
    {
        Dictionary<string, TTranslation> loaded = new(StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.GetFiles(DirectoryPath, "language_*.yml"))
        {
            string language = Path.GetFileNameWithoutExtension(file)["language_".Length..];
            try
            {
                loaded[TranslationManager.NormalizeLanguage(language)] =
                    YamlParser.Deserializer.Deserialize<TTranslation>(File.ReadAllText(file)) ?? new TTranslation();
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Failed to load {Owner} translation '{file}': {ex}", "Translations");
            }
        }

        lock (_sync)
        {
            _translations.Clear();
            foreach ((string language, TTranslation translation) in loaded)
                _translations[language] = translation;
        }

        Reloaded?.Invoke();
    }

    public IReadOnlyList<TranslationValidationResult> Validate()
    {
        TTranslation defaults = new();
        Dictionary<string, string> defaultValues =
            FlattenYaml(YamlParser.Serializer.Serialize(defaults));
        List<TranslationValidationResult> results = new();

        foreach (string file in Directory.GetFiles(DirectoryPath, "language_*.yml"))
        {
            string language = TranslationManager.NormalizeLanguage(
                Path.GetFileNameWithoutExtension(file)["language_".Length..]);
            TranslationValidationResult result = new()
            {
                Owner = Owner,
                Language = language,
                Path = file
            };

            try
            {
                string yaml = File.ReadAllText(file);
                YamlParser.Deserializer.Deserialize<TTranslation>(yaml);
                Dictionary<string, string> values = FlattenYaml(yaml);

                foreach (string key in defaultValues.Keys.Except(values.Keys, StringComparer.OrdinalIgnoreCase))
                    result.MissingKeys.Add(key);

                foreach (string key in values.Keys.Except(defaultValues.Keys, StringComparer.OrdinalIgnoreCase))
                    result.UnknownKeys.Add(key);

                foreach ((string key, string value) in values)
                {
                    HashSet<string> allowedForKey = defaultValues.TryGetValue(key, out string defaultValue)
                        ? PlaceholderRegex.Matches(defaultValue ?? string.Empty)
                            .Select(match => match.Groups[1].Value)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (Match match in PlaceholderRegex.Matches(value ?? string.Empty))
                    {
                        string placeholder = match.Groups[1].Value;
                        if (!allowedForKey.Contains(placeholder) &&
                            !PlaceholderRegistry.Names.Contains(placeholder, StringComparer.OrdinalIgnoreCase))
                            result.UnknownPlaceholders.Add($"{key}: {{{placeholder}}}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            results.Add(result);
        }

        return results;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _reloadTimer?.Dispose();
    }

    private void EnsureDefaultFile()
    {
        string path = Path.Combine(DirectoryPath, $"language_{DefaultLanguage}.yml");
        if (!File.Exists(path))
            File.WriteAllText(path, YamlParser.Serializer.Serialize(new TTranslation()));
    }

    private void OnTranslationFileChanged(object sender, FileSystemEventArgs args)
    {
        lock (_sync)
        {
            _reloadTimer?.Dispose();
            _reloadTimer = new Timer(_ =>
            {
                try
                {
                    Reload();
                    SiteLinkLogger.Info(TranslationManager.Log(
                        "translations.reloaded",
                        new TranslationContext().With("owner", Owner)), "Translations");
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error(TranslationManager.Log(
                        "translations.reload_failed",
                        new TranslationContext()
                            .With("owner", Owner)
                            .With("error", ex)), "Translations");
                }
            }, null, 250, Timeout.Infinite);
        }
    }

    private static Dictionary<string, string> Flatten(object value, string prefix = "")
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (value == null)
            return result;

        if (value is IDictionary<string, string> dictionary)
        {
            foreach ((string key, string dictionaryValue) in dictionary)
                result[string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}"] = dictionaryValue;

            return result;
        }

        foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            object propertyValue = property.GetValue(value);

            if (property.PropertyType == typeof(string))
                result[key] = propertyValue as string;
            else if (propertyValue != null && !property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum)
            {
                foreach ((string childKey, string childValue) in Flatten(propertyValue, key))
                    result[childKey] = childValue;
            }
        }

        return result;
    }

    private static Dictionary<string, string> FlattenYaml(string yaml)
    {
        object root = YamlParser.Deserializer.Deserialize<object>(yaml);
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        FlattenYamlNode(root, string.Empty, result);
        return result;
    }

    private static void FlattenYamlNode(
        object node,
        string prefix,
        Dictionary<string, string> output)
    {
        if (node is IDictionary<object, object> mapping)
        {
            foreach ((object keyObject, object value) in mapping)
            {
                string key = keyObject?.ToString() ?? string.Empty;
                string path = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
                FlattenYamlNode(value, path, output);
            }

            return;
        }

        output[prefix] = node?.ToString();
    }
}
