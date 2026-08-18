namespace SiteLink.API.Testing
{
    public static class ForcedServerFull
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>
            UsersByServer = new(StringComparer.OrdinalIgnoreCase);

        public static void Add(Server server, string userId)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));

            ConcurrentDictionary<string, byte> users = UsersByServer.GetOrAdd(
                server.Name,
                _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

            users[userId] = 0;
        }

        public static bool Remove(Server server, string userId)
        {
            if (server == null || string.IsNullOrWhiteSpace(userId))
                return false;

            if (!UsersByServer.TryGetValue(server.Name, out ConcurrentDictionary<string, byte> users))
                return false;

            bool removed = users.TryRemove(userId, out _);

            if (users.IsEmpty)
                UsersByServer.TryRemove(server.Name, out _);

            return removed;
        }

        public static bool IsForcedFull(Server server, string userId)
        {
            if (server == null || string.IsNullOrWhiteSpace(userId))
                return false;

            return UsersByServer.TryGetValue(server.Name, out ConcurrentDictionary<string, byte> users) &&
                   users.ContainsKey(userId);
        }

        public static IReadOnlyCollection<string> GetUsers(Server server)
        {
            if (server == null ||
                !UsersByServer.TryGetValue(server.Name, out ConcurrentDictionary<string, byte> users))
            {
                return Array.Empty<string>();
            }

            return users.Keys.ToArray();
        }

        public static void Clear()
        {
            UsersByServer.Clear();
        }
    }
}
