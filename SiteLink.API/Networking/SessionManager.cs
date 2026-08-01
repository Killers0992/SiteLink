using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Networking
{
    public class SessionManager
    {
        public static SessionManager Singleton { get; private set; }

        private const double DefaultSessionExpirationSeconds = 10.0;

        /// <summary>
        /// How long a session survives each time its player knocks during a restart and is sent
        /// away again. The client retries every few seconds, so this only has to outlive a
        /// couple of missed attempts - long enough that a slow game server does not cost the
        /// player their seat, short enough that a player who gave up does not hold one.
        /// </summary>
        private const double RestartingClientKeepAliveSeconds = 20.0;

        private readonly Lazy<DisconnectServer> _disconnectServer = new(() => new DisconnectServer());

        public ConcurrentDictionary<string, SessionSlot> Slots { get; } = new();

        public SessionManager()
        {
            Singleton = this;
        }

        public void Update()
        {
            var now = DateTime.UtcNow;

            foreach (var kvp in Slots)
            {
                string userId = kvp.Key;
                var slot = kvp.Value;

                try
                {
                    lock (slot)
                    {
                        if (!IsCurrentSlot(userId, slot))
                            continue;

                        if (slot.Active != null)
                            UpdateOneSession(userId, slot, isPending: false, session: slot.Active, now);

                        if (slot.Pending != null)
                            UpdateOneSession(userId, slot, isPending: true, session: slot.Pending, now);
                    }

                    RemoveSlotIfEmpty(userId, slot);
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error(ex);
                }
            }
        }

        private void UpdateOneSession(string userId, SessionSlot slot, bool isPending, Session session, DateTime now)
        {
            // Attached sessions are driven by Client.PollEvents(), so SessionManager does NOT call Update()
            if (!session.IsDetached)
            {
                // Log re-established once when it flips detached->attached
                if (session.WasDetached)
                {
                    session.WasDetached = false;

                    var detachedAt = session.DetachedAtUtc;
                    session.DetachedAtUtc = null;
                    session.LastExpiryLogSecond = -1;

                    if (!isPending) // pending usually doesn't attach to proxy client
                    {
                        if (detachedAt.HasValue)
                        {
                            var offline = (now - detachedAt.Value).TotalSeconds;

                            SiteLinkLogger.Info($"Session re-established for user (f=yellow){userId}(f=white) (offline (f=green){offline:0.0}s(f=white)).", "Session");
                        }
                        else
                        {
                            SiteLinkLogger.Info($"Session re-established for user {userId}.", "Session");
                        }
                    }
                }

                // While attached, keep alive window extended (mainly for ACTIVE)
                session.AliveUntil = now.AddSeconds(DefaultSessionExpirationSeconds);
                return;
            }

            // Detached sessions ARE driven by SessionManager
            session.Update();

            // Initialize grace window if needed
            if (session.AliveUntil == DateTime.MinValue)
                session.AliveUntil = now.AddSeconds(DefaultSessionExpirationSeconds);

            session.DetachedAtUtc ??= now;
            session.WasDetached = true;

            var remaining = session.AliveUntil - now;
            int remainingSec = (int)Math.Ceiling(remaining.TotalSeconds);

            if (remainingSec > 0)
            {
                // log once per second
                if (session.LastExpiryLogSecond != remainingSec)
                {
                    session.LastExpiryLogSecond = remainingSec;

                    // if (!isPending)
                    //    SiteLinkLogger.Info($"Session for (f=yellow){userId}(f=white) expires in (f=green){remainingSec}s(f=white) (waiting for reconnect)...", "Session");
                }

                return;
            }

            // Expired => destroy (pending should not affect active)
            if (isPending)
            {
                SiteLinkLogger.Info($"Pending session for user (f=yellow){userId}(f=white) expired. Destroying pending session.");
                SafeKill(session, "Pending session expired");
                if (slot.Pending == session) slot.Pending = null;
            }
            else
            {
                SiteLinkLogger.Info($"Active session for user (f=yellow){userId}(f=white) expired (no proxy reconnect). Destroying active session.");
                SafeKill(session, "Active session expired");
                if (slot.Active == session) slot.Active = null;
            }
        }

        private void SafeKill(Session session, string reason)
        {
            if (session == null)
                return;

            try { session.Disconnect(reason); } catch { }
            DisposeSession(session);
        }

        private void DisposeSession(Session session)
        {
            if (session == null)
                return;

            if (_disconnectServer.IsValueCreated)
                _disconnectServer.Value.Cancel(session);

            try { session.Dispose(); } catch { }
        }

        private bool IsCurrentSlot(string userId, SessionSlot slot) =>
            Slots.TryGetValue(userId, out SessionSlot current) && ReferenceEquals(current, slot);

        internal void RemoveSlotIfEmpty(string userId, SessionSlot slot)
        {
            lock (slot)
            {
                if (slot.Active != null || slot.Pending != null || !IsCurrentSlot(userId, slot))
                    return;

                ((ICollection<KeyValuePair<string, SessionSlot>>)Slots).Remove(
                    new KeyValuePair<string, SessionSlot>(userId, slot));
            }
        }

        public Session CreateOrSwitchSession(RemoteConnection connection, Server[] servers, bool silent)
        {
            string userId = connection.PreAuth.UserId;

            while (true)
            {
                SessionSlot slot = Slots.GetOrAdd(userId, _ => new SessionSlot());
                Session replaced = null;
                Session created;

                lock (slot)
                {
                    if (!IsCurrentSlot(userId, slot))
                        continue;

                    if (slot.Active == null && slot.Pending == null)
                    {
                        created = new Session(connection, servers, Thread.CurrentThread.ManagedThreadId, silent);
                        WireSessionCallbacks(created, connection, false);
                        slot.Pending = created;
                    }
                    else if (slot.Active != null && slot.Active.Status == SessionStatus.Connected)
                    {
                        if (slot.Pending != null && silent)
                            return null;

                        replaced = slot.Pending;
                        created = new Session(connection, servers, Thread.CurrentThread.ManagedThreadId, silent);
                        WireSessionCallbacks(created, connection, isPending: true);
                        slot.Pending = created;
                    }
                    else
                    {
                        replaced = slot.Active;
                        created = new Session(connection, servers, Thread.CurrentThread.ManagedThreadId, silent);
                        WireSessionCallbacks(created, connection, isPending: false);
                        slot.Active = created;
                        created.AttachToConnection(connection);
                        connection.Session = created;
                    }
                }

                if (replaced != null)
                {
                    try { replaced.Disconnect(FormatSessionReplaced(replaced)); } catch { }
                    DisposeSession(replaced);
                }

                return created;
            }
        }

        internal bool BeginDisconnect(RemoteConnection connection, string message)
        {
            if (connection?.Request == null || message == null)
                return false;

            string userId = connection.PreAuth.UserId;
            DisconnectServer server = _disconnectServer.Value;

            while (true)
            {
                SessionSlot slot = Slots.GetOrAdd(userId, _ => new SessionSlot());
                Session oldPending;
                Session oldActive;

                lock (slot)
                {
                    if (!IsCurrentSlot(userId, slot))
                        continue;

                    Session session = new Session(
                        connection,
                        new Server[] { server },
                        Thread.CurrentThread.ManagedThreadId,
                        isSilent: true);

                    server.Prepare(session, message);

                    oldPending = slot.Pending;
                    oldActive = slot.Active;
                    slot.Pending = session;
                    slot.Active = null;
                    connection.Session = null;
                }

                DisposeSession(oldPending);
                if (!ReferenceEquals(oldActive, oldPending))
                    DisposeSession(oldActive);

                return true;
            }
        }

        public void PromotePendingToActive(string userId, Session pending)
        {
            Session oldActive;

            if (!Slots.TryGetValue(userId, out SessionSlot slot))
                return;

            lock (slot)
            {
                if (!IsCurrentSlot(userId, slot) || slot.Pending != pending)
                    return;

                oldActive = slot.Active;

                if (oldActive?.Connection != null)
                    oldActive.Connection.IsSwitchingServers = true;

                slot.Active = pending;
                slot.Pending = null;
            }

            if (oldActive != null)
            {
                try { oldActive.Disconnect(FormatSessionReplaced(oldActive)); } catch { }
                DisposeSession(oldActive);
            }

            //SiteLinkLogger.Info($"Promoted pending session to ACTIVE for user {userId}.");
        }

        public void FailPending(string userId, Session pending, string reason)
        {
            if (!Slots.TryGetValue(userId, out SessionSlot slot))
                return;

            lock (slot)
            {
                if (!IsCurrentSlot(userId, slot) || slot.Pending != pending)
                    return;

                slot.Pending = null;
            }

            SiteLinkLogger.Info($"{pending.Connection.Tag} Server (f=yellow){pending.ConnectingToServer.Name}(f=white) is (f=green){reason}(f=white)");
            try { pending.Disconnect(reason); } catch { }
            DisposeSession(pending);
            RemoveSlotIfEmpty(userId, slot);
        }

        /// <summary>
        /// Sends a returning client away again while the game server behind its session is
        /// still restarting, the same way the game server itself delays connections while it
        /// boots: rejection reason 17 plus the number of seconds to wait.
        /// <para>
        /// This is what keeps the player from being lost. Accepting them into a session with no
        /// game server behind it leaves them on a loading screen that never finishes until the
        /// client gives up; sending them away keeps them in their own reconnect loop, which is
        /// the mechanism the game already has for exactly this situation.
        /// </para>
        /// </summary>
        /// <returns>Whether the request was rejected and must not be processed any further.</returns>
        public bool TryDelayRestartingClient(ConnectionRequest request, PreAuth preAuth, NetDataWriter writer)
        {
            if (preAuth.UserId == null || !Slots.TryGetValue(preAuth.UserId, out SessionSlot slot))
                return false;

            byte delay;

            lock (slot)
            {
                Session session = slot.Active;

                if (session == null || !session.IsAwaitingRestartRecovery)
                    return false;

                // The client is provably still waiting, so the session has to outlive this
                // attempt - and the preauth it presented is fresher than the one the session
                // was created with, which matters because a preauth expires.
                session.NoteClientStillWaiting(preAuth, RestartingClientKeepAliveSeconds);

                delay = session.GetConnectionDelaySeconds();
            }

            SiteLinkLogger.Debug(
                $"Delaying (f=yellow){preAuth.UserId}(f=white) by (f=yellow){delay}(f=white) second(s), " +
                $"their server is still restarting.",
                "Session"
            );

            request.RejectWithDelay(writer, delay);
            return true;
        }

        public bool TryReattachConnection(RemoteConnection connection)
        {
            string userId = connection.PreAuth.UserId;

            while (Slots.TryGetValue(userId, out SessionSlot slot))
            {
                lock (slot)
                {
                    if (!IsCurrentSlot(userId, slot))
                        continue;

                    Session s = slot.Active;
                    if (s == null || s.Status != SessionStatus.Connected || s.AliveUntil < DateTime.UtcNow)
                        return false;

                    s.AttachToConnection(connection);
                    connection.Session = s;
                    connection.AcceptRequest();

                    // Normally unreachable: a client whose session is mid-restart is delayed at
                    // the listener before it ever gets here. If it does slip through, there is
                    // no facility to hand over yet - the session sends the handover itself as
                    // soon as its reconnect lands.
                    if (s.IsRestarting)
                        return true;

                    connection.AsServer.Scene("Facility");

                    if (!s.Server.IsSimulated)
                        connection.AsServer.Seed(s.MapSeed);

                    return true;
                }
            }

            return false;
        }

        public void DetachClient(string userId, string reason = null)
        {
            if (!Slots.TryGetValue(userId, out SessionSlot slot))
                return;

            lock (slot)
            {
                if (!IsCurrentSlot(userId, slot) || slot.Active == null)
                    return;

                slot.Active.DetachFromConnection();

                // A client sent away by a game server restart is on its own countdown, which
                // can be far longer than the default grace. The session has to outlive that
                // countdown or the player loses their place while the vanilla restart screen
                // is still counting down for them.
                DateTime grace = DateTime.UtcNow.AddSeconds(DefaultSessionExpirationSeconds);

                slot.Active.AliveUntil = slot.Active.ReconnectDeadline > grace
                    ? slot.Active.ReconnectDeadline
                    : grace;
            }

            //SiteLinkLogger.Info($"Session detached for {userId} {reason}, expires in {DefaultSessionExpirationSeconds}s...");
        }

        private void WireSessionCallbacks(Session session, RemoteConnection connection, bool isPending)
        {
            session.OnServerFull += resp =>
            {
                // final means no more servers to try
                if (!resp.IsFinalResponse) return;

                if (isPending)
                {
                    ClientConnectionResponseEvent ev = new ClientConnectionResponseEvent(connection, session.ConnectingToServer, new ServerIsFullResponse());
                    EventManager.Client.InvokeConnectionResponse(ev);

                    if (!ev.IsCancelled && !session.IsSilent)
                    {
                        connection.AsServer.Hint(
                            FormatServerMessage(
                                TranslationManager.For(session).Connection.ServerFullHint,
                                resp.Server,
                                session),
                            3f);
                    }

                    // keep active, kill pending
                    FailPending(connection.PreAuth.UserId, session, $"full");
                    return;
                }

                // ACTIVE first join: reject if still pending, otherwise disconnect
                RejectOrDisconnect(
                    connection,
                    FormatServerMessage(
                        TranslationManager.For(session).Connection.ServerFullDisconnect,
                        resp.Server,
                        session));
            };

            session.OnServerOffline += resp =>
            {
                if (!resp.IsFinalResponse)
                    return;

                if (isPending)
                {
                    ClientConnectionResponseEvent ev = new ClientConnectionResponseEvent(connection, session.ConnectingToServer, new ServerIsOfflineResponse());
                    EventManager.Client.InvokeConnectionResponse(ev);

                    if (!ev.IsCancelled && !session.IsSilent)
                    {
                        connection.AsServer.Hint(
                            FormatServerMessage(
                                TranslationManager.For(session).Connection.ServerOfflineHint,
                                resp.Server,
                                session),
                            3f);
                    }

                    FailPending(connection.PreAuth.UserId, session, $"offline");
                    return;
                }

                RejectOrDisconnect(
                    connection,
                    FormatServerMessage(
                        TranslationManager.For(session).Connection.ServerOfflineDisconnect,
                        resp.Server,
                        session));
            };

            session.OnBanned += ban =>
            {
                if (isPending)
                {
                    connection.AsServer.Hint(
                        FormatBanMessage(TranslationManager.For(session).Connection.BannedHint, ban, session),
                        5f);
                    FailPending(connection.PreAuth.UserId, session, $"Banned: {ban.Reason}");
                    return;
                }

                RejectOrDisconnect(
                    connection,
                    FormatBanMessage(TranslationManager.For(session).Connection.BannedDisconnect, ban, session));
            };

            session.OnConnectionDelayed += delay =>
            {
                if (!isPending || connection.Request != null)
                    return;

                if (Slots.TryGetValue(connection.PreAuth.UserId, out SessionSlot currentSlot) &&
                    currentSlot.Active != null)
                {
                    currentSlot.Active.ShowConnectionDelayedStatus(delay.Server, delay.Offset);
                }
            };
        }

        private void RejectOrDisconnect(Connection connection, string reason)
        {
            connection.Disconnect(reason);
        }

        private static string FormatServerMessage(string template, Server server, Session session) =>
            TranslationManager.Format(template, TranslationContext.For(session, server))
                .Add("server", server?.DisplayName)
                .Add("server_name", server?.Name)
                .Format();

        private static string FormatBanMessage(string template, Session.BannedResponse ban, Session session) =>
            TranslationManager.Format(template, TranslationContext.For(session, ban.Server))
                .Add("server", ban.Server?.DisplayName)
                .Add("server_name", ban.Server?.Name)
                .Add("reason", ban.Reason)
                .Add("expires", ban.Expires, "g")
                .Format();

        private static string FormatSessionReplaced(Session session) =>
            TranslationManager.Format(
                TranslationManager.For(session).Connection.SessionReplaced,
                TranslationContext.For(session)).Format();

        public void DestroyAllForUser(string userId, string reason)
        {
            while (Slots.TryGetValue(userId, out SessionSlot slot))
            {
                Session pending;
                Session active;

                lock (slot)
                {
                    if (!IsCurrentSlot(userId, slot))
                        continue;

                    pending = slot.Pending;
                    active = slot.Active;
                    slot.Pending = null;
                    slot.Active = null;
                    RemoveSlotIfEmpty(userId, slot);
                }

                SafeKill(pending, reason);
                if (!ReferenceEquals(active, pending))
                    SafeKill(active, reason);
                return;
            }
        }
    }
}
