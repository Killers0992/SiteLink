using System;
using System.Collections.Generic;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;
using LiteNetLib;
using SiteLink.API;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Connects this game server to every configured SiteLink proxy and keeps their player
    /// count and round state accurate.
    /// </summary>
    public class SiteLinkBridgePlugin : Plugin<BridgeConfig>
    {
        public static SiteLinkBridgePlugin Instance { get; private set; }

        public override string Name => "SiteLink.Bridge";

        public override string Description => "Connects this game server to SiteLink proxies and reports its player count and round state.";

        public override string Author => "Killers0992";

        public override Version Version => typeof(SiteLinkBridgePlugin).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        public override void Enable()
        {
            Instance = this;

            if (Config == null)
            {
                Logger.Error("[SiteLink.Bridge] Config is null, not starting the bridge.");
                return;
            }

            List<BridgeEndpoint> endpoints = BuildEndpoints();

            if (endpoints.Count == 0)
            {
                Logger.Error("[SiteLink.Bridge] No usable entries under 'proxies', not starting the bridge.");
                return;
            }

            SiteLinkBridge.RegisterConnectedHandler(OnConnected);
            SiteLinkBridge.RegisterDisconnectedHandler(OnDisconnected);

            try
            {
                SiteLinkBridge.Initialize(endpoints);
            }
            catch (Exception ex)
            {
                SiteLinkBridge.UnregisterConnectedHandler(OnConnected);
                SiteLinkBridge.UnregisterDisconnectedHandler(OnDisconnected);

                Logger.Error($"[SiteLink.Bridge] Failed to initialize the bridge: {ex}");
                return;
            }

            PlayerCountReporter.Start();
            RoundStateReporter.Start();

            Logger.Info($"[SiteLink.Bridge] Connecting to {endpoints.Count} proxy/proxies: {FormatEndpoints(endpoints)}");
        }

        public override void Disable()
        {
            RoundStateReporter.Stop();
            PlayerCountReporter.Stop();

            SiteLinkBridge.UnregisterConnectedHandler(OnConnected);
            SiteLinkBridge.UnregisterDisconnectedHandler(OnDisconnected);

            Instance = null;

            Logger.Info("[SiteLink.Bridge] Disabled.");
        }

        /// <summary>
        /// Turns the configured proxy list into endpoints, dropping entries that cannot work
        /// instead of letting the bridge retry against them forever.
        /// </summary>
        private List<BridgeEndpoint> BuildEndpoints()
        {
            List<BridgeEndpoint> endpoints = new List<BridgeEndpoint>();

            if (Config.Proxies == null)
                return endpoints;

            foreach (ProxyEntry entry in Config.Proxies)
            {
                if (entry == null)
                    continue;

                if (string.IsNullOrEmpty(entry.Ip))
                {
                    Logger.Warn("[SiteLink.Bridge] Skipping a proxy entry with an empty 'ip'.");
                    continue;
                }

                if (entry.Port <= 0 || entry.Port > 65535)
                {
                    Logger.Warn($"[SiteLink.Bridge] Skipping proxy '{entry.Ip}': port {entry.Port} is out of range.");
                    continue;
                }

                bool duplicate = false;

                foreach (BridgeEndpoint existing in endpoints)
                {
                    if (existing.Ip == entry.Ip && existing.Port == entry.Port)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    Logger.Warn($"[SiteLink.Bridge] Skipping duplicate proxy entry {entry}.");
                    continue;
                }

                if (string.IsNullOrEmpty(entry.SecretKey))
                    Logger.Warn($"[SiteLink.Bridge] Proxy {entry} has an empty 'secret_key'; it will reject this bridge.");

                endpoints.Add(new BridgeEndpoint(entry.Ip, entry.Port, entry.SecretKey));
            }

            return endpoints;
        }

        private static string FormatEndpoints(List<BridgeEndpoint> endpoints)
        {
            string[] parts = new string[endpoints.Count];

            for (int i = 0; i < endpoints.Count; i++)
                parts[i] = endpoints[i].ToString();

            return string.Join(", ", parts);
        }

        private void OnConnected(BridgeEndpoint endpoint)
        {
            if (Config != null && Config.Debug)
                Logger.Info($"[SiteLink.Bridge] Connected to proxy {endpoint}.");

            // Report immediately so the proxy is not stuck on an unknown count until the
            // first heartbeat.
            try
            {
                PlayerCountReporter.ReportTo(endpoint);
            }
            catch (Exception ex)
            {
                Logger.Error($"[SiteLink.Bridge] Initial player count report to {endpoint} failed: {ex}");
            }
        }

        private void OnDisconnected(BridgeEndpoint endpoint, DisconnectInfo info)
        {
            // ConnectionRejected is always a configuration problem, never a transient one, so
            // it is worth a line even with debug off - otherwise the bridge retries forever
            // in silence and nobody learns why.
            if (info.Reason == DisconnectReason.ConnectionRejected)
            {
                Logger.Warn($"[SiteLink.Bridge] Proxy {endpoint} rejected the bridge. Its 'secret_key' ({endpoint.SecretKey?.Length ?? 0} characters) has to match 'bridge.secret_key' of a server with 'bridge.enabled: true' in that proxy's settings.yml. Retrying...");
                return;
            }

            if (Config != null && Config.Debug)
                Logger.Warn($"[SiteLink.Bridge] Disconnected from proxy {endpoint}: {info.Reason}. Retrying...");
        }

        /// <summary>
        /// Routine chatter - periodic reports and state churn. These fire every second on a
        /// busy server, so they only reach the console when 'debug' is on.
        /// </summary>
        internal static void LogDebug(string message) =>
            Logger.Debug($"[SiteLink.Bridge] {message}", Instance?.Config?.Debug ?? false);

        internal static void LogWarn(string message) => Logger.Warn($"[SiteLink.Bridge] {message}");

        internal static void LogError(string message) => Logger.Error($"[SiteLink.Bridge] {message}");
    }
}
