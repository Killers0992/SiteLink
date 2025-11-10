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
}
