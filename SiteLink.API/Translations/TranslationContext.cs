namespace SiteLink.API.Translations;

/// <summary>
/// Runtime data available to translation placeholders.
/// Plugins can attach additional values with <see cref="With"/>.
/// </summary>
public sealed class TranslationContext
{
    private readonly Dictionary<string, object> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public Session Session { get; init; }
    public Server Server { get; init; }
    public Plugin Plugin { get; init; }

    public IReadOnlyDictionary<string, object> Values => _values;

    public TranslationContext With(string name, object value)
    {
        if (!string.IsNullOrWhiteSpace(name))
            _values[name.Trim().Trim('{', '}')] = value;

        return this;
    }

    public bool TryGetValue(string name, out object value) =>
        _values.TryGetValue(name, out value);

    public static TranslationContext For(Session session = null, Server server = null, Plugin plugin = null) =>
        new()
        {
            Session = session,
            Server = server ?? session?.Server,
            Plugin = plugin
        };
}
