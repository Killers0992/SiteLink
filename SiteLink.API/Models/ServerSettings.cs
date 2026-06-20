using System.ComponentModel;

namespace SiteLink.API.Models;

/// <summary>
/// Represents a backend server configuration entry managed by SiteLink.
/// Each instance defines how SiteLink connects to and exposes a specific game server.
/// </summary>
public class ServerSettings
{
    /// <summary>
    /// The internal name of the server.
    /// Used by plugins and internal systems to reference this server uniquely.
    /// </summary>
    [Description("Internal server name used by plugins and configuration to reference this server.")]
    public string Name { get; set; } = "default";

    /// <summary>
    /// The user-visible name of the server, displayed in menus and server selectors.
    /// </summary>
    [Description("Display name shown to players.")]
    public string DisplayName { get; set; } = "<color=white>Default</color>";

    /// <summary>
    /// The IP address or hostname of the backend server this proxy should connect to.
    /// </summary>
    [Description("The backend server's IP address or hostname.")]
    public string Address { get; set; } = "127.0.0.1";

    /// <summary>
    /// The port number of the backend server this proxy should connect to.
    /// </summary>
    [Description("The port number of the backend server.")]
    public int Port { get; set; } = 7778;

    /// <summary>
    /// The maximum number of clients that can connect to this backend server.
    /// </summary>
    [Description("Maximum number of players that can connect to this server.")]
    public int MaxClients { get; set; } = 25;

    /// <summary>
    /// Determines whether the proxy should forward the real player IP address to the backend server.
    /// </summary>
    [Description("If true, forwards the player's real IP address to the backend server.")]
    public bool ForwardIpAddress { get; set; } = false;

    /// <summary>
    /// A list of fallback server names that players should be redirected to if this server is unavailable.
    /// </summary>
    [Description("List of fallback servers used when this server is unavailable.")]
    public string[] FallbackServers { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Number of times SiteLink should try to reconnect a player to this server after it shuts down,
    /// before attempting the configured fallback servers.
    /// </summary>
    [Description("Number of reconnect attempts after this server shuts down, before attempting fallback servers. Set to 0 to try fallbacks immediately.")]
    public int ShutdownRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Delay between reconnect attempts after this server shuts down.
    /// </summary>
    [Description("Seconds between reconnect attempts after this server shuts down.")]
    public float ShutdownRetryInterval { get; set; } = 10f;

    /// <summary>
    /// Message shown while the player remains connected to the proxy and waits for this server to return.
    /// </summary>
    [Description("Hint shown while waiting for a shut down server. Supports {server}, {server_name}, {attempts}, and {interval}.")]
    public string ShutdownWaitingMessage { get; set; } =
        "[SiteLink]\nServer {server} shutdown, waiting for server to be online...";

    /// <summary>
    /// Message shown when all reconnect attempts have failed.
    /// </summary>
    [Description("Hint shown after all shutdown reconnect attempts fail. Supports {server}, {server_name}, {attempts}, and {interval}.")]
    public string ShutdownUnreachableMessage { get; set; } =
        "[SiteLink]\nServer {server} is not reachable!";

    /// <summary>
    /// Gets or sets the configuration settings for the bridge.
    /// </summary>
    [Description("If server uses Bridge plugin then configure it below. ( this allows you to communicate between labapi plugins <-> sitelink plugins )")]
    public BridgeSettings Bridge { get; set; } = new BridgeSettings();
}
