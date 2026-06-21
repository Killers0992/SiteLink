using SiteLink.API.Events;
using SiteLink.API.Events.Args;
using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Networking
{
    public class SessionManager
    {
        public static SessionManager Singleton { get; private set; }

        private const double DefaultSessionExpirationSeconds = 10.0;

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
                    if (slot.Active != null)
                        UpdateOneSession(userId, slot, isPending: false, session: slot.Active, now);

                    if (slot.Pending != null)
                        UpdateOneSession(userId, slot, isPending: true, session: slot.Pending, now);

                    if (slot.Active == null && slot.Pending == null)
                        Slots.TryRemove(userId, out _);
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
            try { session.Disconnect(reason); } catch { }
            try { session.Dispose(); } catch { }
        }

        public Session CreateOrSwitchSession(RemoteConnection connection, Server[] servers, bool silent)
        {
            string userId = connection.PreAuth.UserId;

            SessionSlot slot = Slots.GetOrAdd(userId, _ => new SessionSlot());

            if (slot.Active == null && slot.Pending == null)
            {
                slot.Pending = new Session(connection, servers, Thread.CurrentThread.ManagedThreadId, silent);

                WireSessionCallbacks(slot.Pending, connection, false);

                return slot.Pending;
            }

            // If there is an active connected session, create a pending one
            if (slot.Active != null && slot.Active.Status == SessionStatus.Connected)
            {
                // If pending sessions exists and its silent prevent from creating new one right away.
                if (slot.Pending != null && silent)
                    return null;

                // Dispose any old pending attempt
                if (slot.Pending != null)
                    slot.Pending.Disconnect(FormatSessionReplaced(slot.Pending));
                slot.Pending?.Dispose();

                slot.Pending = new Session(connection, servers, Thread.CurrentThread.ManagedThreadId, silent);

                WireSessionCallbacks(slot.Pending, connection, isPending: true);

                //SiteLinkLogger.Info($"{connection.Tag} Created PENDING session to switch servers.");

                return slot.Pending;
            }

            // Otherwise create/replace active
            if (slot.Active != null)
                slot.Active.Disconnect(FormatSessionReplaced(slot.Active));
            slot.Active?.Dispose();

            slot.Active = new Session(connection, servers, Thread.CurrentThread.ManagedThreadId, silent);

            WireSessionCallbacks(slot.Active, connection, isPending: false);

            slot.Active.AttachToConnection(connection);

            //SiteLinkLogger.Info($"{connection.Tag} Created ACTIVE session.");

            connection.Session = slot.Active;

            return slot.Active;
        }

        public void PromotePendingToActive(string userId, Session pending)
        {
            if (!Slots.TryGetValue(userId, out SessionSlot slot))
                return;

            if (slot.Pending != pending)
                return;

            Session oldActive = slot.Active;

            if (oldActive != null)
                oldActive.Connection.IsSwitchingServers = true;

            slot.Active = pending;
            slot.Pending = null;

            if (oldActive != null)
                oldActive.Disconnect(FormatSessionReplaced(oldActive));
            oldActive?.Dispose();

            //SiteLinkLogger.Info($"Promoted pending session to ACTIVE for user {userId}.");
        }

        public void FailPending(string userId, Session pending, string reason)
        {
            if (!Slots.TryGetValue(userId, out SessionSlot slot))
                return;


            if (slot.Pending != pending)
                return;

            SiteLinkLogger.Info($"{pending.Connection.Tag} Server (f=yellow){pending.ConnectingToServer.Name}(f=white) is (f=green){reason}(f=white)");
            slot.Pending = null;

            pending.Disconnect(reason);
            pending.Dispose();
        }

        public bool TryReattachConnection(RemoteConnection connection)
        {
            if (!Slots.TryGetValue(connection.PreAuth.UserId, out var slot) || slot.Active == null)
                return false;

            Session s = slot.Active;

            if (s.Status != SessionStatus.Connected || s.AliveUntil < DateTime.UtcNow)
                return false;

            s.AttachToConnection(connection);

            connection.AcceptRequest();
            connection.Session = s;

            connection.AsServer.Scene("Facility");

            if (!s.Server.IsSimulated)
                connection.AsServer.Seed(s.MapSeed);

            return true;
        }

        public void DetachClient(string userId, string reason = null)
        {
            if (!Slots.TryGetValue(userId, out var slot) || slot.Active == null)
                return;

            slot.Active.DetachFromConnection();
            slot.Active.AliveUntil = DateTime.UtcNow.AddSeconds(DefaultSessionExpirationSeconds);

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
            if (!Slots.TryGetValue(userId, out var slot))
                return;

            if (slot.Pending != null)
            {
                SafeKill(slot.Pending, reason);
                slot.Pending = null;
            }

            if (slot.Active != null)
            {
                SafeKill(slot.Active, reason);
                slot.Active = null;
            }

            Slots.TryRemove(userId, out _);
        }
    }
}
