using System.ComponentModel;

namespace SiteLink.API.Models;

/// <summary>
/// Configuration of the single UDP endpoint every bridge plugin connects to.
/// <para>
/// Bridges are routed to their game server by the secret key they present, not by the port
/// they connected to, so any number of game servers share this one endpoint.
/// </para>
/// </summary>
public class BridgeListenerSettings
{
    /// <summary>
    /// Whether the proxy opens a dedicated endpoint for bridge plugins.
    /// </summary>
    [Description("Opens a dedicated endpoint that every bridge plugin connects to.")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The internal name of the bridge listener, used in logs.
    /// </summary>
    [Description("Internal identifier for the bridge listener, used in logs.")]
    public string Name { get; set; } = "bridge";

    /// <summary>
    /// The IP address the bridge endpoint binds to. Use <c>0.0.0.0</c> for every interface.
    /// </summary>
    [Description("Local IP address the bridge endpoint binds to (use 0.0.0.0 to bind to all interfaces).")]
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// The UDP port bridge plugins connect to. Every game server uses this same port and is
    /// told apart by its secret key.
    /// </summary>
    [Description("UDP port every bridge plugin connects to, regardless of how many game servers there are.")]
    public int ListenPort { get; set; } = 7900;
}
