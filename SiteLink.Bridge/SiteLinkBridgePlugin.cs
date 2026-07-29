using System;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;
using LiteNetLib;
using SiteLink.API;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Connects this game server to a SiteLink proxy and keeps the proxy's player count
    /// accurate.
    /// </summary>
    public class SiteLinkBridgePlugin : Plugin<BridgeConfig>
    {
        public static SiteLinkBridgePlugin Instance { get; private set; }

        public override string Name => "SiteLink.Bridge";

        public override string Description => "Connects this game server to a SiteLink proxy and reports its player count.";

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

            SiteLinkBridge.RegisterConnectedHandler(OnConnected);
            SiteLinkBridge.RegisterDisconnectedHandler(OnDisconnected);

            try
            {
                SiteLinkBridge.Initialize(Config.Ip, Config.Port, Config.SecretKey);
            }
            catch (Exception ex)
            {
                SiteLinkBridge.UnregisterConnectedHandler(OnConnected);
                SiteLinkBridge.UnregisterDisconnectedHandler(OnDisconnected);

                Logger.Error($"[SiteLink.Bridge] Failed to initialize the bridge: {ex}");
                return;
            }

            PlayerCountReporter.Start();

            Logger.Info($"[SiteLink.Bridge] Connecting to proxy {Config.Ip}:{Config.Port}...");
        }

        public override void Disable()
        {
            PlayerCountReporter.Stop();

            SiteLinkBridge.UnregisterConnectedHandler(OnConnected);
            SiteLinkBridge.UnregisterDisconnectedHandler(OnDisconnected);

            Instance = null;

            Logger.Info("[SiteLink.Bridge] Disabled.");
        }

        private void OnConnected()
        {
            if (Config != null && Config.Debug)
                Logger.Info($"[SiteLink.Bridge] Connected to proxy {Config.Ip}:{Config.Port}.");

            // Report immediately so the proxy is not stuck on an unknown count until the
            // first heartbeat.
            try
            {
                PlayerCountReporter.Report();
            }
            catch (Exception ex)
            {
                Logger.Error($"[SiteLink.Bridge] Initial player count report failed: {ex}");
            }
        }

        private void OnDisconnected(DisconnectInfo info)
        {
            if (Config != null && Config.Debug)
                Logger.Warn($"[SiteLink.Bridge] Disconnected from proxy: {info.Reason}. Retrying...");
        }

        internal static void Log(string message) => Logger.Info($"[SiteLink.Bridge] {message}");

        internal static void LogError(string message) => Logger.Error($"[SiteLink.Bridge] {message}");
    }
}
