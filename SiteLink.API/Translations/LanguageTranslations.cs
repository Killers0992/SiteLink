using System.ComponentModel;

namespace SiteLink.API.Translations;

public class LanguageTranslations
{
    [Description("Shared values used by global placeholders.")]
    public GeneralTranslations General { get; set; } = new();

    [Description("Messages used while connecting to backend servers.")]
    public ConnectionTranslations Connection { get; set; } = new();

    [Description("Messages used when a backend server shuts down or restarts.")]
    public RecoveryTranslations Recovery { get; set; } = new();

    [Description("Messages printed by proxy commands.")]
    public Dictionary<string, string> Commands { get; set; } = ConsoleTranslationDefaults.Commands();

    [Description("Messages printed by proxy services and managers.")]
    public Dictionary<string, string> Logs { get; set; } = ConsoleTranslationDefaults.Logs();
}

public class GeneralTranslations
{
    [Description("Global {tag} placeholder used in player-facing messages.")]
    public string Tag { get; set; } = "[SiteLink]";
}

public class ConnectionTranslations
{
    [Description("Placeholders: {server}, {server_name}")]
    public string ServerNotFound { get; set; } = "{tag}\nServer {server_name} was not found.";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerRemoved { get; set; } = "{tag}\nServer {server} was removed from the configuration.";

    [Description("No placeholders.")]
    public string ExpiredAuthentication { get; set; } = "{tag}\nYour authentication token expired.";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerFullHint { get; set; } = "Server <color=orange>{server}</color> is full!";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerFullDisconnect { get; set; } = "{tag}\nServer {server} is full.";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerOfflineHint { get; set; } = "Server <color=orange>{server}</color> is offline!";

    [Description("Placeholders: {server}, {server_name}")]
    public string ServerOfflineDisconnect { get; set; } = "{tag}\nServer {server} is offline.";

    [Description("Placeholders: {server}, {server_name}, {reason}, {expires}")]
    public string BannedHint { get; set; } = "Banned from <color=orange>{server}</color>: {reason}";

    [Description("Placeholders: {server}, {server_name}, {reason}, {expires}")]
    public string BannedDisconnect { get; set; } =
        "{tag}\nBanned from {server}: {reason}\nExpires: {expires}";

    [Description("Placeholders: {server}, {server_name}, {delay}")]
    public string ConnectionDelayed { get; set; } =
        "{tag}\nServer {server} delayed the connection by {delay} seconds...";

    [Description("No placeholders. Internal transition reason; normally not shown.")]
    public string SessionReplaced { get; set; } =
        "{tag}\nYour server session was replaced.";
}

public class RecoveryTranslations
{
    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string ShutdownWaiting { get; set; } =
        "{tag}\nServer {server} shut down, waiting for it to be online...";

    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string ShutdownUnreachable { get; set; } = "{tag}\nServer {server} is not reachable!";

    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string RestartWaiting { get; set; } =
        "{tag}\nServer {server} is restarting, reconnecting in {restart_delay} seconds...";

    [Description("Placeholders: {server}, {server_name}, {attempts}, {interval}, {restart_delay}")]
    public string RestartUnreachable { get; set; } =
        "{tag}\nServer {server} did not come back online!";
}

internal static class ConsoleTranslationDefaults
{
    public static Dictionary<string, string> Commands() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["unknown"] = "Unknown command. Type 'help' to see a list of available commands.",
        ["failed"] = "Failed executing command: {error}",
        ["help.header"] = "Available commands:",
        ["language.usage"] = "Usage: language <userId> <language|default> | language list | language reload | language validate",
        ["language.available"] = "Available languages: {languages}",
        ["language.changed"] = "Language for {user_id} changed to {language}.",
        ["language.reset"] = "Language for {user_id} reset to the proxy default.",
        ["language.not_found"] = "Language '{language}' is not available. Available: {languages}",
        ["language.reloaded"] = "Translations reloaded.",
        ["language.valid"] = "All translation files are valid.",
        ["language.invalid"] = "Translation validation found {count} issue(s):\n{issues}",
        ["reload.complete"] = "Settings, plugins, and translations reloaded.",
        ["player.not_found"] = "Player with user ID {user_id} was not found.",
        ["server.not_found"] = "Server '{server_name}' was not found.",
        ["listener.not_found"] = "Listener '{listener}' was not found.",
        ["kick.usage"] = "Usage: kick <userId|all> <reason>",
        ["kick.complete"] = "Kicked {count} client(s) with reason: {reason}",
        ["position.usage"] = "Usage: position <userId>",
        ["position.result"] = "Current position for {user_id}: {position}; horizontal {horizontal}; vertical {vertical}.",
        ["send.usage"] = "Usage: send <all|player|server> <targetServer>",
        ["effect.usage"] = "Usage: effect <userId> <effectId> <value>",
        ["effect.complete"] = "Effect updated.",
        ["connstats.usage"] = "Usage: connstats <userId>",
        ["connstats.result"] = "Connection Details:\n - User ID: {user_id}\n - IP Address: {ip}\n - Client Version: {client_version}\n\nStatistics:\n - Connected At: {connected_at}\n - Duration: {duration}\n - Last Activity: {last_activity}\n\nNetwork Traffic:\n - Bytes Sent: {bytes_sent}\n - Bytes Received: {bytes_received}\n - Total Traffic: {total_bytes}\n\nPackets:\n - Packets Sent: {packets_sent}\n - Packets Received: {packets_received}\n - Total Packets: {total_packets}\n\nSession:\n{session}",
        ["connstats.session"] = " - Server: {server_name}\n - Session Uptime: {uptime}\n - To Server: {to_server}\n - From Server: {from_server}\n - Reconnections: {reconnections}\n - Server Switches: {switches}",
        ["connstats.no_session"] = " - No active session",
        ["central.usage"] = "Usage: central <listenerName> <command>",
        ["spawntext.usage"] = "Usage: spawntext <userId> <message>",
        ["help.entry"] = " - {command}",
        ["listeners.header"] = "Listeners:",
        ["listeners.entry"] = " - {address}:{port} [{connections}] ({listener})",
        ["servers.header"] = "Servers:",
        ["servers.entry"] = " - {server_name} [{online}] ({endpoint})",
        ["players.header"] = "Players on servers:",
        ["players.server"] = " - Server {server_name} [{count}] ({endpoint})",
        ["players.entry"] = "   [{player_id}] {user_id}; connection time {duration}",
        ["send.complete"] = "Sent {count} client(s) to {server_name}.",
        ["send.complete_from"] = "Sent {count} client(s) from {source_server} to {server_name}.",
        ["send.same_server"] = "The source and destination servers cannot be the same.",
        ["send.invalid_source"] = "Use a player ID or server name as the source.",
        ["stats.usage"] = "Usage: stats [system|services|listeners|connections|sessions]",
        ["stats.system"] = "System Statistics:\n - Uptime: {uptime}\n - Memory: {memory} MB\n - Total Transferred: {bytes}\n - Active Connections: {connections}\n - Active Listeners: {listeners}",
        ["stats.services"] = "Service Statistics:\n{services}",
        ["stats.service"] = " {service}:\n  - Iterations: {iterations}\n  - Avg Time: {average} ms\n  - CPU: {cpu}%\n  - Queue Depth: {queue}\n  - Items Processed: {processed}",
        ["stats.listeners"] = "Listener Statistics:\n{listeners}",
        ["stats.listener"] = " - {listener} [{address}:{port}]\n   Connections: {connections} | Errors: {errors}\n   Traffic Sent: {sent}\n   Traffic Received: {received}\n   Packets: {packets} | Uptime: {uptime}",
        ["stats.connections"] = "Connection Statistics:\n{connections}",
        ["stats.no_connections"] = " No active connections.",
        ["stats.connection"] = " - {user_id} [{duration}]\n   Sent: {sent} | Received: {received}\n   Packets: {packets} | Session: {server_name}",
        ["stats.sessions"] = "Session Statistics:\n{sessions}",
        ["stats.server"] = " - Server {server_name} [{count} sessions]\n{players}",
        ["stats.session"] = "   - {user_id}\n     To Server: {sent} | From Server: {received}\n     Uptime: {uptime} | Reconnections: {reconnections}",
        ["plugins.usage"] = "Usage: plugins [install owner/repository|check|update]",
        ["plugins.list"] = "Installed plugins:\n{plugins}",
        ["plugins.installed"] = "Installed {repository} to {path}. Restart the proxy to load it.",
        ["plugins.current"] = "All installed plugins are current.",
        ["plugins.updates"] = "{count} plugin update(s) available:\n{updates}",
        ["plugins.updated"] = "Installed {count} plugin update(s). Restart the proxy to load them:\n{updates}",
        ["plugins.failed"] = "Plugin operation failed: {error}",
        ["plugins.update_available"] = "Plugin {plugin} has an update: {current} -> {latest}.",
        ["update.version"] = "SiteLink {version}; API {api_version}; game {game_version}.",
        ["update.current"] = "SiteLink is up to date.",
        ["update.available"] = "SiteLink {latest} is available (current: {current}). Type 'update' to install it.",
        ["update.installing"] = "Installing SiteLink {latest} over {current}; the proxy will restart.",
        ["update.failed"] = "Update failed: {error}",
        ["update.check_failed"] = "Could not check for updates: {error}"
    };

    public static Dictionary<string, string> Logs() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["translations.reloaded"] = "Hot reloaded translations for {owner}.",
        ["translations.reload_failed"] = "Hot reload failed for {owner}: {error}",
        ["plugins.loading_dependencies"] = "Loading {count} dependencies...",
        ["plugins.loaded_dependencies"] = "Loaded {loaded}/{count} dependencies.",
        ["plugins.loading"] = "Loading {count} plugins...",
        ["plugins.loaded"] = "Loaded {loaded}/{count} plugins.",
        ["plugins.plugin_loading"] = "[{current}/{count}] Plugin '{plugin}' is loading...",
        ["plugins.plugin_loaded"] = "[{current}/{count}] Plugin '{plugin}' loaded successfully.",
        ["plugins.plugin_failed"] = "[{current}/{count}] Plugin '{plugin}' failed to load:\n{error}",
        ["commands.registering"] = "Registering commands...",
        ["commands.registered"] = "Registered {count} commands.",
        ["commands.registered_one"] = "Command '{command}' registered.",
        ["commands.duplicate"] = "Command '{command}' is already registered; skipping duplicate."
    };
}
