using System.ComponentModel;

namespace SiteLink.API.Misc;

/// <summary>
/// Represents the global configuration for SiteLink.
/// Handles loading, saving, and reloading proxy settings from a YAML file.
/// </summary>
public class SiteLinkSettings
{
    /// <summary>
    /// The default path to the YAML configuration file.
    /// </summary>
    public const string SettingsPath = "settings.yml";

    /// <summary>
    /// The globally accessible, loaded instance of the proxy settings.
    /// </summary>
    public static SiteLinkSettings Singleton { get; private set; }

    /// <summary>
    /// Loads the proxy configuration from disk.
    /// Creates a new file with default values if it does not exist.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(SettingsPath))
            File.WriteAllText(SettingsPath, YamlParser.Serializer.Serialize(new SiteLinkSettings()));

        string settingsContent = File.ReadAllText(SettingsPath);

        try
        {
            Singleton = YamlParser.Deserializer.Deserialize<SiteLinkSettings>(settingsContent);
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error($"Failed to deserialize config! {ex}");
            return;
        }

        Save();
    }

    /// <summary>
    /// Reloads the proxy configuration from disk.
    /// </summary>
    public static void Reload() => Load();

    /// <summary>
    /// Saves the current proxy configuration to disk.
    /// </summary>
    public static void Save()
    {
        File.WriteAllText(SettingsPath, YamlParser.Serializer.Serialize(Singleton));
    }

    /// <summary>
    /// The maximum number of players allowed across all connected servers.
    /// Set to -1 to disable the limit.
    /// </summary>
    [Description("Maximum player limit across all servers. Use -1 for unlimited.")]
    public int PlayerLimit { get; set; } = -1;

    /// <summary>
    /// The list of listeners that define how SiteLink accepts player connections.
    /// Each listener can have its own port, game version, and server list configuration.
    /// </summary>
    [Description("List of listeners defining how the proxy accepts player connections.")]
    public List<ListenerSettings> Listeners { get; set; } = new List<ListenerSettings>()
    {
        new ListenerSettings()
    };

    /// <summary>
    /// The list of backend servers managed by SiteLink.
    /// These entries define connection targets and display names for each proxied server.
    /// </summary>
    [Description("List of backend servers that the proxy connects players to.")]
    public List<ServerSettings> Servers { get; set; } = new List<ServerSettings>()
    {
        new ServerSettings()
    };

    /// <summary>
    /// The ordered list of server names (from <see cref="Servers"/>) that appear in the server selector menu.
    /// </summary>
    [Description("List of server names displayed in the in-game server selector menu.")]
    public string[] ServersInSelector { get; set; } = ["default"];

    /// <summary>
    /// The number of times the system will attempt to reconnect to the backend server
    /// when a temporary disconnection occurs (e.g., during restarts or map changes).
    /// If the server cannot be reached after this many attempts, the proxy will connect
    /// the player to a fallback server (if specified) or disconnect them from the proxy.
    /// </summary>
    [Description("Number of automatic reconnect attempts before redirecting to a fallback server or disconnecting.")]
    public int MaximumReconnectAttempts { get; set; } = 5;
}