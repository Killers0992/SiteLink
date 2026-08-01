using System;
using System.Diagnostics;
using LabApi.Features.Wrappers;
using UnityEngine;
using SiteLink.API;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Reports how many real players this game server is hosting to every connected proxy.
    /// <para>
    /// A proxy cannot count this itself: with more than one proxy in front of the same game
    /// server, each proxy only sees the sessions it owns. CSG 5.6 requires the number
    /// reported to the central servers to be accurate, so the game server is the only
    /// authority.
    /// </para>
    /// </summary>
    public static class PlayerCountReporter
    {
        private const float MinimumInterval = 1f;

        private static GameObject _tickerObject;
        private static bool _running;

        /// <summary>Player count sent in the last report, or -1 if nothing was sent yet.</summary>
        public static int LastReportedCount { get; private set; } = -1;

        /// <summary>Slot count sent in the last report, or -1 if nothing was sent yet.</summary>
        public static int LastReportedMax { get; private set; } = -1;

        /// <summary>Number of entries in <c>Player.List</c> at the last count, dummies and host included.</summary>
        public static int LastRawCount { get; private set; } = -1;

        /// <summary>Number of dummies excluded at the last count.</summary>
        public static int LastDummyCount { get; private set; } = -1;

        public static void Start()
        {
            if (_running)
                return;

            _running = true;

            _tickerObject = new GameObject("SiteLinkBridgeTicker");
            UnityEngine.Object.DontDestroyOnLoad(_tickerObject);
            _tickerObject.AddComponent<PlayerCountTicker>();
        }

        public static void Stop()
        {
            _running = false;

            if (_tickerObject != null)
            {
                UnityEngine.Object.Destroy(_tickerObject);
                _tickerObject = null;
            }

            LastReportedCount = -1;
            LastReportedMax = -1;
            LastRawCount = -1;
            LastDummyCount = -1;
        }

        internal static float Interval
        {
            get
            {
                BridgeConfig config = SiteLinkBridgePlugin.Instance?.Config;

                if (config == null)
                    return 5f;

                return config.PlayerCountReportInterval < MinimumInterval
                    ? MinimumInterval
                    : config.PlayerCountReportInterval;
            }
        }

        /// <summary>
        /// Counts the players that should be reported to the central servers.
        /// The host is not a player, and neither are dummies - counting them would report
        /// numbers that do not correspond to anyone actually playing.
        /// </summary>
        public static int CountPlayers(out int raw, out int dummies)
        {
            raw = 0;
            dummies = 0;

            int counted = 0;

            foreach (Player player in Player.List)
            {
                raw++;

                if (player == null || player.IsHost)
                    continue;

                if (player.IsDummy)
                {
                    dummies++;
                    continue;
                }

                counted++;
            }

            return counted;
        }

        /// <summary>
        /// Counts the current players and pushes them to every connected proxy. Sending
        /// unconditionally on a timer rather than only on change means a bridge reconnect
        /// heals itself without any extra handshake.
        /// </summary>
        public static void Report()
        {
            if (!SiteLinkBridge.IsConnected)
                return;

            BridgeConfig config = SiteLinkBridgePlugin.Instance?.Config;

            if (config != null && !config.ReportPlayerCount)
                return;

            int players = CountPlayers(out int raw, out int dummies);
            int maxPlayers = GetMaxPlayers();

            LastRawCount = raw;
            LastDummyCount = dummies;

            bool changed = players != LastReportedCount || maxPlayers != LastReportedMax;

            LastReportedCount = players;
            LastReportedMax = maxPlayers;

            SiteLinkBridge.Send(SiteLinkBridge.MsgPlayerCount, writer =>
            {
                writer.Put(players);
                writer.Put(maxPlayers);
            });

            if (changed)
                SiteLinkBridgePlugin.LogDebug($"Reported {players}/{maxPlayers} players to the proxies (excluded {dummies} dummies).");
        }

        /// <summary>
        /// Reports the current count to one specific proxy. Used when a proxy connects late:
        /// the other proxies are already up to date and must not be told again just to catch
        /// this one up.
        /// </summary>
        public static void ReportTo(BridgeEndpoint endpoint)
        {
            BridgeConfig config = SiteLinkBridgePlugin.Instance?.Config;

            if (config != null && !config.ReportPlayerCount)
                return;

            int players = CountPlayers(out int raw, out int dummies);
            int maxPlayers = GetMaxPlayers();

            LastRawCount = raw;
            LastDummyCount = dummies;
            LastReportedCount = players;
            LastReportedMax = maxPlayers;

            SiteLinkBridge.SendTo(endpoint, SiteLinkBridge.MsgPlayerCount, writer =>
            {
                writer.Put(players);
                writer.Put(maxPlayers);
            });

            SiteLinkBridgePlugin.LogDebug($"Reported {players}/{maxPlayers} players to {endpoint} (excluded {dummies} dummies).");
        }

        private static int GetMaxPlayers()
        {
            try
            {
                return LabApi.Features.Wrappers.Server.MaxPlayers;
            }
            catch
            {
                return 0;
            }
        }

        private sealed class PlayerCountTicker : MonoBehaviour
        {
            /// <summary>
            /// Round state is polled far more often than the player count. Nothing raises an
            /// event when the transport stops delaying connections, and every second spent
            /// not noticing is a second the proxy keeps players staring at a frozen facility.
            /// </summary>
            private const double RoundStatePollSeconds = 1d;

            // Idle mode sets Time.timeScale to 0.01, and InvokeRepeating runs on scaled time,
            // so a 5 second interval turned into 500 real seconds the moment the server went
            // idle. The proxy then hit its 30 second bridge timeout and fell back to counting
            // its own sessions. A stopwatch is the only clock idle mode cannot slow down.
            private readonly Stopwatch _sinceLastTick = Stopwatch.StartNew();
            private readonly Stopwatch _sinceLastRoundState = Stopwatch.StartNew();

            private void Update()
            {
                if (_sinceLastRoundState.Elapsed.TotalSeconds >= RoundStatePollSeconds)
                {
                    _sinceLastRoundState.Restart();

                    try
                    {
                        // Idle mode and the connection delay flag have no events to hook, so
                        // they ride along on this timer.
                        RoundStateReporter.Poll();
                    }
                    catch (Exception ex)
                    {
                        SiteLinkBridgePlugin.LogError($"Round state report failed: {ex}");
                    }
                }

                if (_sinceLastTick.Elapsed.TotalSeconds < Interval)
                    return;

                _sinceLastTick.Restart();

                try
                {
                    Report();
                }
                catch (Exception ex)
                {
                    // A throwing report must not kill the ticker, otherwise the proxy
                    // silently falls back to its own inaccurate count forever.
                    SiteLinkBridgePlugin.LogError($"Player count report failed: {ex}");
                }
            }
        }
    }
}
