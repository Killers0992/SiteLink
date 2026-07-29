using System.Collections.Generic;
using System.ComponentModel;

namespace SiteLink.Bridge
{
    /// <summary>
    /// A single SiteLink proxy this game server should connect to.
    /// </summary>
    public class ProxyEntry
    {
        [Description("Address of the SiteLink proxy.")]
        public string Ip { get; set; } = "127.0.0.1";

        [Description("Bridge port of the SiteLink proxy. This is the proxy's dedicated bridge endpoint, not a game client listener port - every game server behind that proxy uses the same one.")]
        public int Port { get; set; } = 7900;

        [Description("Shared secret. Must match 'secret_key' under this server's bridge settings in that proxy's config.")]
        public string SecretKey { get; set; } = "---";

        public override string ToString() => $"{Ip}:{Port}";
    }

    /// <summary>
    /// Configuration for <see cref="SiteLinkBridgePlugin"/>.
    /// LabAPI serializes these with the underscored naming convention, so
    /// <c>SecretKey</c> becomes <c>secret_key</c> in the YAML file.
    /// </summary>
    public class BridgeConfig
    {
        [Description("Every SiteLink proxy this game server sits behind. Each one gets the player count and round state, because a proxy only ever sees the sessions it owns itself.")]
        public List<ProxyEntry> Proxies { get; set; } = new List<ProxyEntry>
        {
            new ProxyEntry(),
        };

        [Description("Print connection state changes, player count reports and round state reports to the server console.")]
        public bool Debug { get; set; } = false;

        [Description("How often, in seconds, the current player count is reported to the proxies. Values below 1 are clamped.")]
        public float PlayerCountReportInterval { get; set; } = 5f;

        [Description("Report the player count to the proxies. Disabling this makes a proxy fall back to its own session count, which is inaccurate when more than one proxy is used.")]
        public bool ReportPlayerCount { get; set; } = true;

        [Description("Report round state (waiting, in progress, ended, restarting, idle) to the proxies instead of leaving them to guess it from relayed traffic.")]
        public bool ReportRoundState { get; set; } = true;
    }
}
