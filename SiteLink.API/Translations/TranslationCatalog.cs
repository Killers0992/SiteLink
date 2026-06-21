using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        _watcher = new FileSystemWatcher(DirectoryPath, "language_*.json")
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

        foreach (string file in Directory.GetFiles(DirectoryPath, "language_*.json"))
        {
            string language = Path.GetFileNameWithoutExtension(file)["language_".Length..];
            try
            {
                loaded[TranslationManager.NormalizeLanguage(language)] =
                    JsonConvert.DeserializeObject<TTranslation>(File.ReadAllText(file)) ?? new TTranslation();
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
        Dictionary<string, string> defaultValues = FlattenJson(
            JsonConvert.SerializeObject(defaults));
        List<TranslationValidationResult> results = new();

        foreach (string file in Directory.GetFiles(DirectoryPath, "language_*.json"))
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
                string json = File.ReadAllText(file);
                JsonConvert.DeserializeObject<TTranslation>(json);
                Dictionary<string, string> values = FlattenJson(json);

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
        string path = Path.Combine(DirectoryPath, $"language_{DefaultLanguage}.json");
        if (!File.Exists(path))
            File.WriteAllText(
                path,
                JsonConvert.SerializeObject(new TTranslation(), Formatting.Indented));
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

    private static Dictionary<string, string> FlattenJson(string json)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        FlattenJsonNode(JToken.Parse(json), string.Empty, result);
        return result;
    }

    private static void FlattenJsonNode(
        JToken node,
        string prefix,
        Dictionary<string, string> output)
    {
        if (node is JObject obj)
        {
            foreach (JProperty property in obj.Properties())
            {
                string path = string.IsNullOrEmpty(prefix)
                    ? property.Name
                    : $"{prefix}.{property.Name}";
                FlattenJsonNode(property.Value, path, output);
            }
            return;
        }

        output[prefix] = node.Type == JTokenType.Null ? null : node.ToString();
    }
}
