using System;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using RoundRestarting;
using SiteLink.API;

namespace SiteLink.Bridge
{
    /// <summary>
    /// Tells the proxy what the game server's round is actually doing.
    /// <para>
    /// The proxy used to infer this from the <c>RoundRestartMessage</c> of whichever session
    /// it happened to be relaying, which is a guess: an empty server restarting produced no
    /// message at all, and a soft restart looked identical to nothing happening. The game
    /// server knows, so the game server says so.
    /// </para>
    /// </summary>
    public static class RoundStateReporter
    {
        private static bool _running;
        private static bool _subscribed;

        /// <summary>Set by the round-ended event, cleared once a new round starts or restarts.</summary>
        private static bool _roundEnded;

        /// <summary>
        /// Own restart flag. <c>RoundRestart.IsRoundRestarting</c> is only cleared by the
        /// client-side hook, which a dedicated server never runs, so it is not trustworthy
        /// as an "is it over yet" signal.
        /// </summary>
        private static bool _restarting;

        /// <summary>Restart kind captured when the restart began.</summary>
        private static BridgeRestartType _restartType = BridgeRestartType.None;

        private static bool _shuttingDown;

        private static BridgeRoundState _lastState = BridgeRoundState.Unknown;
        private static BridgeRestartType _lastRestartType = BridgeRestartType.None;
        private static bool _lastIdle;
        private static bool _lastAccepting;
        private static bool _sentOnce;

        /// <summary>The state sent to the proxy in the last report.</summary>
        public static BridgeRoundState LastState => _lastState;

        /// <summary>The restart type sent to the proxy in the last report.</summary>
        public static BridgeRestartType LastRestartType => _lastRestartType;

        /// <summary>Whether the last report said the server is idling.</summary>
        public static bool LastIdle => _lastIdle;

        /// <summary>Whether the last report said the transport is accepting connections.</summary>
        public static bool LastAcceptingConnections => _lastAccepting;

        public static void Start()
        {
            if (_running)
                return;

            _running = true;
            _roundEnded = false;
            _restarting = false;
            _restartType = BridgeRestartType.None;
            _shuttingDown = false;

            Subscribe();

            SiteLinkBridge.RegisterConnectedHandler(OnProxyConnected);

            // Nothing has been sent yet, so the first Report() must go out even if the
            // computed state happens to equal the default.
            _sentOnce = false;
            Report();
        }

        public static void Stop()
        {
            if (!_running)
                return;

            _running = false;

            SiteLinkBridge.UnregisterConnectedHandler(OnProxyConnected);

            Unsubscribe();

            _lastState = BridgeRoundState.Unknown;
            _lastRestartType = BridgeRestartType.None;
            _lastIdle = false;
            _sentOnce = false;
        }

        private static void Subscribe()
        {
            if (_subscribed)
                return;

            ServerEvents.WaitingForPlayers += OnWaitingForPlayers;
            ServerEvents.RoundStarted += OnRoundStarted;
            ServerEvents.RoundEnded += OnRoundEnded;
            ServerEvents.RoundRestarted += OnRoundRestarted;
            ServerEvents.Shutdown += OnShutdown;

            // Fires for every restart kind, including `sr` and fast restart, before the
            // server actually tears the round down.
            RoundRestart.OnRestartTriggered += OnRestartTriggered;

            _subscribed = true;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            ServerEvents.WaitingForPlayers -= OnWaitingForPlayers;
            ServerEvents.RoundStarted -= OnRoundStarted;
            ServerEvents.RoundEnded -= OnRoundEnded;
            ServerEvents.RoundRestarted -= OnRoundRestarted;
            ServerEvents.Shutdown -= OnShutdown;

            RoundRestart.OnRestartTriggered -= OnRestartTriggered;

            _subscribed = false;
        }

        private static void OnWaitingForPlayers()
        {
            _roundEnded = false;
            _restarting = false;
            _restartType = BridgeRestartType.None;
            Report();
        }

        private static void OnRoundStarted()
        {
            _roundEnded = false;
            _restarting = false;
            _restartType = BridgeRestartType.None;
            Report();
        }

        private static void OnRoundEnded(RoundEndedEventArgs args)
        {
            _roundEnded = true;
            Report();
        }

        /// <summary>
        /// Despite the name, the game fires this at the very start of
        /// <c>InitiateRoundRestart</c>, before it has torn anything down. It means "a restart
        /// was requested", which is exactly the moment the proxy wants to hear about.
        /// </summary>
        private static void OnRoundRestarted() => BeginRestart();

        private static void OnRestartTriggered() => BeginRestart();

        private static void BeginRestart()
        {
            _roundEnded = false;

            if (!_restarting)
            {
                _restarting = true;

                // The game reads this flag when it decides which RoundRestartMessage to
                // send, so reading it now yields the same answer the clients get.
                _restartType = CustomNetworkManager.EnableFastRestart
                    ? BridgeRestartType.Fast
                    : BridgeRestartType.Full;
            }

            Report();
        }

        private static void OnShutdown()
        {
            _shuttingDown = true;
            Report();
        }

        private static void OnProxyConnected(BridgeEndpoint endpoint)
        {
            if (!IsEnabled())
                return;

            // A bridge that connects mid-round would otherwise leave the proxy on Unknown
            // until the next round event, which on a quiet server can be a long time.
            SendTo(endpoint, GetState(), GetRestartType(), IsIdle(), IsAcceptingConnections(), GetConnectionDelay());
        }

        /// <summary>
        /// Polled alongside the player count, because idle mode has no event to hook.
        /// </summary>
        internal static void Poll()
        {
            if (!_running)
                return;

            Report();
        }

        /// <summary>
        /// Computes the current state and pushes it to every connected proxy when it
        /// changed. Restart states are always sent, since they are short-lived and missing
        /// one is worse than sending it twice.
        /// </summary>
        public static void Report()
        {
            if (!IsEnabled() || !SiteLinkBridge.IsConnected)
                return;

            BridgeRoundState state = GetState();
            BridgeRestartType restartType = GetRestartType();
            bool idle = IsIdle();
            bool accepting = IsAcceptingConnections();
            byte delaySeconds = GetConnectionDelay();

            bool changed = !_sentOnce
                || state != _lastState
                || restartType != _lastRestartType
                || idle != _lastIdle
                || accepting != _lastAccepting;

            if (!changed)
                return;

            _lastState = state;
            _lastRestartType = restartType;
            _lastIdle = idle;
            _lastAccepting = accepting;
            _sentOnce = true;

            SiteLinkBridge.Send(SiteLinkBridge.MsgRoundState, writer =>
            {
                writer.Put((byte)state);
                writer.Put((byte)restartType);
                writer.Put(idle);
                writer.Put(accepting);
                writer.Put(delaySeconds);
            });

            SiteLinkBridgePlugin.LogDebug($"Reported round state {state} (restart: {restartType}, idle: {idle}, accepting: {accepting}) to the proxy.");
        }

        private static void SendTo(BridgeEndpoint endpoint, BridgeRoundState state, BridgeRestartType restartType, bool idle, bool accepting, byte delaySeconds)
        {
            SiteLinkBridge.SendTo(endpoint, SiteLinkBridge.MsgRoundState, writer =>
            {
                writer.Put((byte)state);
                writer.Put((byte)restartType);
                writer.Put(idle);
                writer.Put(accepting);
                writer.Put(delaySeconds);
            });
        }

        private static BridgeRoundState GetState()
        {
            if (_shuttingDown)
                return BridgeRoundState.Shutdown;

            if (_restarting || RoundRestart.IsRoundRestarting)
                return BridgeRoundState.Restarting;

            if (RoundSummary.RoundInProgress())
                return BridgeRoundState.InProgress;

            return _roundEnded
                ? BridgeRoundState.Ended
                : BridgeRoundState.WaitingForPlayers;
        }

        private static BridgeRestartType GetRestartType()
        {
            if (_restarting || RoundRestart.IsRoundRestarting)
            {
                return _restartType == BridgeRestartType.None
                    ? (CustomNetworkManager.EnableFastRestart ? BridgeRestartType.Fast : BridgeRestartType.Full)
                    : _restartType;
            }

            return BridgeRestartType.None;
        }

        private static bool IsEnabled()
        {
            BridgeConfig config = SiteLinkBridgePlugin.Instance?.Config;

            return config == null || config.ReportRoundState;
        }

        private static bool IsIdle()
        {
            try
            {
                return IdleMode.IdleModeActive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the transport will let a player in right now.
        /// <para>
        /// The game server sets <c>DelayConnections</c> for the whole restart and clears it
        /// only once it is ready, rejecting every preauth in between. The proxy used to guess
        /// this with a fixed ten second wait; reporting the flag directly is the difference
        /// between reconnecting when the server is up and reconnecting when a timer says so.
        /// </para>
        /// </summary>
        private static bool IsAcceptingConnections()
        {
            try
            {
                if (_shuttingDown)
                    return false;

                return !CustomLiteNetLib4MirrorTransport.DelayConnections;
            }
            catch (Exception)
            {
                // Never claim the server is closed because a field moved; the proxy falls
                // back to its own timers when we say "accepting" and it turns out not to be.
                return true;
            }
        }

        /// <summary>How long the game server delays incoming connections by, clamped to a byte.</summary>
        private static byte GetConnectionDelay()
        {
            try
            {
                byte delay = CustomLiteNetLib4MirrorTransport.DelayTime;

                return delay == 0 ? (byte)1 : delay;
            }
            catch (Exception)
            {
                return 3;
            }
        }
    }
}
