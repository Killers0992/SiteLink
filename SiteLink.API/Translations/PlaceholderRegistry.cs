namespace SiteLink.API.Translations;

public delegate object PlaceholderResolver(TranslationContext context);

/// <summary>
/// Global placeholder registry shared by the proxy and all plugins.
/// </summary>
public static class PlaceholderRegistry
{
    private static readonly ConcurrentDictionary<string, PlaceholderResolver> Resolvers =
        new(StringComparer.OrdinalIgnoreCase);

    static PlaceholderRegistry()
    {
        Register("tag", context => TranslationManager.For(context.Session).General.Tag);
        Register("username", context => context.Session?.Nickname ?? string.Empty);
        Register("user_id", context => context.Session?.UserId ?? string.Empty);
        Register("player_id", context => context.Session?.NetworkId ?? 0);
        Register("language", context => TranslationManager.GetLanguage(context.Session));
        Register("server", context => context.Server?.DisplayName ?? string.Empty);
        Register("server_name", context => context.Server?.Name ?? string.Empty);
        Register("online", context => context.Server?.SessionsCount ?? 0);
        Register("max_players", context => context.Server?.MaxSessions ?? 0);
        Register("plugin", context => context.Plugin?.Name ?? string.Empty);
        Register("plugin_version", context => context.Plugin?.Version?.ToString() ?? string.Empty);
    }

    public static IEnumerable<string> Names => Resolvers.Keys.OrderBy(name => name);

    public static void Register(string name, PlaceholderResolver resolver, bool replace = true)
    {
        string normalized = Normalize(name);
        if (normalized.Length == 0)
            throw new ArgumentException("Placeholder name cannot be empty.", nameof(name));

        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));

        if (replace)
            Resolvers[normalized] = resolver;
        else if (!Resolvers.TryAdd(normalized, resolver))
            throw new InvalidOperationException($"Placeholder '{{{normalized}}}' is already registered.");
    }

    public static bool Unregister(string name) =>
        Resolvers.TryRemove(Normalize(name), out _);

    public static bool TryResolve(string name, TranslationContext context, out object value)
    {
        string normalized = Normalize(name);

        if (context?.TryGetValue(normalized, out value) == true)
            return true;

        if (Resolvers.TryGetValue(normalized, out PlaceholderResolver resolver))
        {
            value = resolver(context ?? new TranslationContext());
            return true;
        }

        value = null;
        return false;
    }

    private static string Normalize(string name) =>
        name?.Trim().Trim('{', '}').ToLowerInvariant() ?? string.Empty;
}
