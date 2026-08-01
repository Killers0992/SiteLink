using System;
using System.Collections.Generic;
using System.Text;
using CommandSystem;
using SiteLink.API;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Reports the state of the bridge: which proxies are connected, what it last told them,
    /// and which target servers they advertised.
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class BridgeStatusCommand : ICommand
    {
        public string Command => "slbridge";

        public string[] Aliases => new[] { "sitelinkbridge" };

        public string Description => "Shows the SiteLink bridge connection state, the player count and the round state reported to the proxies.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            StringBuilder builder = new StringBuilder();

            List<BridgeEndpoint> endpoints = SiteLinkBridge.Endpoints;

            builder.AppendLine("SiteLink bridge status");
            builder.AppendLine($"  connected: {SiteLinkBridge.ConnectedCount}/{endpoints.Count} proxies");

            if (endpoints.Count == 0)
            {
                builder.AppendLine("  proxies: <plugin not enabled or no usable entries>");
            }
            else
            {
                builder.AppendLine("  proxies:");
                foreach (BridgeEndpoint endpoint in endpoints)
                {
                    builder.AppendLine($"    - {endpoint}: {(SiteLinkBridge.IsConnectedTo(endpoint) ? "connected" : "disconnected")}");
                }
            }

            int players = PlayerCountReporter.CountPlayers(out int raw, out int dummies);

            builder.AppendLine($"  players now: counted={players} raw={raw} dummy={dummies}");
            builder.AppendLine($"  last reported: {FormatReported()}");
            builder.AppendLine($"  round state: {RoundStateReporter.LastState} (restart: {RoundStateReporter.LastRestartType}, idle: {RoundStateReporter.LastIdle})");

            List<string> servers = SiteLinkBridge.TargetServers;

            if (servers == null || servers.Count == 0)
            {
                builder.AppendLine("  target servers: none received from the proxies");
            }
            else
            {
                builder.AppendLine($"  target servers ({servers.Count}):");
                foreach (string server in servers)
                    builder.AppendLine($"    - {server}");
            }

            response = builder.ToString();
            return true;
        }

        private static string FormatReported()
        {
            if (PlayerCountReporter.LastReportedCount < 0)
                return "nothing reported yet";

            return $"{PlayerCountReporter.LastReportedCount}/{PlayerCountReporter.LastReportedMax}";
        }
    }
}
