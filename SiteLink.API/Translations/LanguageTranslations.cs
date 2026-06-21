using System.ComponentModel;

namespace SiteLink.API.Translations;

public class LanguageTranslations
{
    [Description("Messages used while connecting to backend servers.")]
    public ConnectionTranslations Connection { get; set; } = new();

    [Description("Messages used when a backend server shuts down or restarts.")]
    public RecoveryTranslations Recovery { get; set; } = new();
}

public class ConnectionTranslations
{
    [Description("Placeholders: {server}, {server_name}")]
    public string ServerNotFound { get; set; } = "[SiteLink]\nServer {server_name} was not found.";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerRemoved { get; set; } = "[SiteLink]\nServer {server} was removed from the configuration.";

    [Description("No placeholders.")]
    public string ExpiredAuthentication { get; set; } = "[SiteLink]\nYour authentication token expired.";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerFullHint { get; set; } = "Server <color=orange>{server}</color> is full!";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerFullDisconnect { get; set; } = "[SiteLink]\nServer {server} is full.";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerOfflineHint { get; set; } = "Server <color=orange>{server}</color> is offline!";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerOfflineDisconnect { get; set; } = "[SiteLink]\nServer {server} is offline.";

    [Description("Placeholders: {server}, {server_name}, {reason}, {expires}")]
    public string BannedHint { get; set; } = "Banned from <color=orange>{server}</color>: {reason}";

    [Description("Placeholders: {server}, {server_name}, {reason}, {expires}")]
    public string BannedDisconnect { get; set; } =
        "[SiteLink]\nBanned from {server}: {reason}\nExpires: {expires}";

    [Description("Placeholders: {server}, {server_name}, {delay}")]
    public string ConnectionDelayed { get; set; } =
        "[SiteLink]\nServer {server} delayed the connection by {delay} seconds...";

    [Description("No placeholders. Internal transition reason; normally not shown.")]
    public string SessionReplaced { get; set; } =
        "[SiteLink]\nYour server session was replaced.";
}

public class RecoveryTranslations
{
    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string ShutdownWaiting { get; set; } =
        "[SiteLink]\nServer {server} shut down, waiting for it to be online...";

    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string ShutdownUnreachable { get; set; } = "[SiteLink]\nServer {server} is not reachable!";

    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string RestartWaiting { get; set; } =
        "[SiteLink]\nServer {server} is restarting, reconnecting in {restart_delay} seconds...";

    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string RestartUnreachable { get; set; } =
        "[SiteLink]\nServer {server} did not come back online!";
}
