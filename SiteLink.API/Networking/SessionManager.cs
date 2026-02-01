namespace SiteLink.API.Networking
{
    public class SessionManager
    {
        // Sessions expire after 10 seconds by default
        private const double DefaultSessionExpirationSeconds = 10.0;

        public ConcurrentDictionary<string, SessionSlot> Slots { get; } = new();

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
                            SiteLinkLogger.Info($"Session re-established for user (f=yellow){userId}(f=white) (offline (f=green){offline:0.0}s(f=white))).", "Session");
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

                    if (!isPending)
                        SiteLinkLogger.Info($"Session for (f=yellow){userId}(f=white) expires in (f=green){remainingSec}s(f=white) (waiting for reconnect)...", "Session");
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

        public Session CreateOrSwitchSession(Connection connection, Server[] servers)
        {
            string userId = connection.PreAuth.UserId;

            SessionSlot slot = Slots.GetOrAdd(userId, _ => new SessionSlot());

            // If there is an active connected session, create a pending one
            if (slot.Active != null && slot.Active.Status == SessionStatus.Connected)
            {
                // Dispose any old pending attempt
                slot.Pending?.Disconnect("Replaced by newer pending session");
                slot.Pending?.Dispose();

                slot.Pending = new Session(connection, servers);
                WireSessionCallbacks(slot.Pending, connection, isPending: true);

                //SiteLinkLogger.Info($"{connection.Tag} Created PENDING session to switch servers.");

                return slot.Pending;
            }

            // Otherwise create/replace active
            slot.Active?.Disconnect("Active session replaced");
            slot.Active?.Dispose();

            slot.Active = new Session(connection, servers);
            WireSessionCallbacks(slot.Active, connection, isPending: false);

            slot.Active.AttachToConnection(connection);

            SiteLinkLogger.Info($"{connection.Tag} Created ACTIVE session.");

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

            oldActive.Connection.IsSwitchingServers = true;

            slot.Active = pending;
            slot.Pending = null;

            oldActive?.Disconnect("Replaced by new active session");
            oldActive?.Dispose();

            //SiteLinkLogger.Info($"Promoted pending session to ACTIVE for user {userId}.");
        }

        public void FailPending(string userId, Session pending, string reason)
        {
            if (!Slots.TryGetValue(userId, out SessionSlot slot))
                return;

            if (slot.Pending != pending)
                return;

            SiteLinkLogger.Info($"{pending.Connection.Tag} Server (f=yellow){pending.ConnectingToServer.Name}(f=white) is (f=green){reason}");
            slot.Pending = null;

            pending.Disconnect(reason);
            pending.Dispose();
        }

        public bool TryReattachConnection(Connection connection)
        {
            if (!Slots.TryGetValue(connection.PreAuth.UserId, out var slot) || slot.Active == null)
                return false;

            Session s = slot.Active;

            if (s.Status != SessionStatus.Connected || s.AliveUntil < DateTime.UtcNow)
                return false;

            s.AttachToConnection(connection);
            connection.Session = s;

            connection.AsServer.Scene("Facility");
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

        private void WireSessionCallbacks(Session session, Connection connection, bool isPending)
        {
            session.OnServerFull += resp =>
            {
                // final means no more servers to try
                if (!resp.IsFinalResponse) return;

                if (isPending)
                {
                    // keep active, kill pending
                    connection.AsServer.Hint($"Server <color=orange>{resp.Server.Name}</color> is full!", 3f);
                    FailPending(connection.PreAuth.UserId, session, $"full");
                    return;
                }

                // ACTIVE first join: reject if still pending, otherwise disconnect
                RejectOrDisconnect(connection, $"Server {resp.Server.Name} is full");
            };

            session.OnServerOffline += resp =>
            {
                if (!resp.IsFinalResponse) 
                    return;

                if (isPending)
                {
                    connection.AsServer.Hint($"Server <color=orange>{resp.Server.Name}</color> is offline!", 3f);
                    FailPending(connection.PreAuth.UserId, session, $"Server {resp.Server.Name} is offline");
                    return;
                }

                RejectOrDisconnect(connection, $"Server {resp.Server.Name} is offline");
            };

            session.OnBanned += ban =>
            {
                if (isPending)
                {
                    connection.AsServer.Hint($"Banned from <color=orange>{ban.Server.Name}</color>: {ban.Reason}", 5f);
                    FailPending(connection.PreAuth.UserId, session, $"Banned: {ban.Reason}");
                    return;
                }

                RejectOrDisconnect(connection, $"Banned from {ban.Server.Name}: {ban.Reason} (until {ban.Expires})");
            };

            session.OnConnectionDelayed += delay =>
            {
                if (isPending && connection.Request == null)
                    connection.AsServer.Hint($"Server <color=orange>{delay.Server.Name}</color> delayed connection, retrying...", 3f);
            };
        }

        private void RejectOrDisconnect(Connection connection, string reason)
        {
            connection.Disconnect(reason);
        }

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

            SiteLinkLogger.Info($"Destroyed session for {userId}: {reason}", "Session");
        }
    }
}
