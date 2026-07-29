using System.ComponentModel;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Configuration for <see cref="SiteLinkBridgePlugin"/>.
    /// LabAPI serializes these with the underscored naming convention, so
    /// <c>SecretKey</c> becomes <c>secret_key</c> in the YAML file.
    /// </summary>
    public class BridgeConfig
    {
        [Description("Address of the SiteLink proxy this game server should connect to.")]
        public string Ip { get; set; } = "127.0.0.1";

        [Description("Port of the SiteLink proxy this game server should connect to.")]
        public int Port { get; set; } = 7777;

        [Description("Shared secret. Must match 'secret_key' under the server's bridge settings in the proxy config.")]
        public string SecretKey { get; set; } = "---";

        [Description("Print connection state changes and player count reports to the server console.")]
        public bool Debug { get; set; } = true;

        [Description("How often, in seconds, the current player count is reported to the proxy. Values below 1 are clamped.")]
        public float PlayerCountReportInterval { get; set; } = 5f;

        [Description("Report the player count to the proxy. Disabling this makes the proxy fall back to its own session count, which is inaccurate when more than one proxy is used.")]
        public bool ReportPlayerCount { get; set; } = true;
    }
}
