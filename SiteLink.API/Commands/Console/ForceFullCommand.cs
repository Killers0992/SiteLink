using SiteLink.API.Testing;
using System.Text;

namespace SiteLink.API.Commands.Console
{
    public static class ForceFullCommand
    {
        [ConsoleCommand("forcefull")]
        public static void OnForceFullCommand(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string action = args[0].ToLowerInvariant();

            switch (action)
            {
                case "add":
                case "on":
                    Add(args);
                    return;

                case "remove":
                case "off":
                    Remove(args);
                    return;

                case "list":
                    List(args);
                    return;

                case "clear":
                    ForcedServerFull.Clear();
                    SiteLinkLogger.Info("Cleared all forced ServerFull test entries.", "forcefull");
                    return;

                default:
                    PrintUsage();
                    return;
            }
        }

        private static void Add(string[] args)
        {
            if (args.Length < 3)
            {
                SiteLinkLogger.Info(
                    "Usage: forcefull add <server> <userId>",
                    "forcefull");
                return;
            }

            Server server = Server.Get<Server>(name: args[1]);

            if (server == null)
            {
                SiteLinkLogger.Info(
                    $"Server '{args[1]}' was not found.",
                    "forcefull");
                return;
            }

            string userId = args[2];

            ForcedServerFull.Add(server, userId);

            SiteLinkLogger.Info(
                $"Forced ServerFull enabled for user {userId} on server {server.Name}.",
                "forcefull");
        }

        private static void Remove(string[] args)
        {
            if (args.Length < 3)
            {
                SiteLinkLogger.Info(
                    "Usage: forcefull remove <server> <userId>",
                    "forcefull");
                return;
            }

            Server server = Server.Get<Server>(name: args[1]);

            if (server == null)
            {
                SiteLinkLogger.Info(
                    $"Server '{args[1]}' was not found.",
                    "forcefull");
                return;
            }

            string userId = args[2];

            bool removed = ForcedServerFull.Remove(server, userId);

            SiteLinkLogger.Info(
                removed
                    ? $"Forced ServerFull disabled for user {userId} on server {server.Name}."
                    : $"No forced ServerFull entry existed for user {userId} on server {server.Name}.",
                "forcefull");
        }

        private static void List(string[] args)
        {
            if (args.Length < 2)
            {
                SiteLinkLogger.Info(
                    "Usage: forcefull list <server>",
                    "forcefull");
                return;
            }

            Server server = Server.Get<Server>(name: args[1]);

            if (server == null)
            {
                SiteLinkLogger.Info(
                    $"Server '{args[1]}' was not found.",
                    "forcefull");
                return;
            }

            IReadOnlyCollection<string> users = ForcedServerFull.GetUsers(server);

            if (users.Count == 0)
            {
                SiteLinkLogger.Info(
                    $"No users are forced full on server {server.Name}.",
                    "forcefull");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine($"Forced ServerFull users for {server.Name}:");

            foreach (string user in users)
                sb.AppendLine($" - {user}");

            SiteLinkLogger.Info(sb.ToString(), "forcefull");
        }

        private static void PrintUsage()
        {
            SiteLinkLogger.Info("""
            forcefull add <server> <userId>
            forcefull remove <server> <userId>
            forcefull list <server>
            forcefull clear

            Aliases:
            forcefull on <server> <userId>
            forcefull off <server> <userId>
            """, "forcefull");
        }
    }
}
