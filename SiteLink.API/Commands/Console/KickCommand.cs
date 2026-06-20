using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Commands;

public class KickCommand
{
    [ConsoleCommand("kick")]
    public static void OnKickCommand(string[] args)
    {
        if (args.Length < 1)
        {
            SiteLinkLogger.Info("Syntax: kick <userid> <reason>", "kick");
            return;
        }

        string userId = args[0];
        string[] text = args.Skip(1).ToArray();

        switch (userId.ToLower())
        {
            case "all":
                int kicked = 0;
                foreach (RemoteConnection con in RemoteConnection.ConnectionByUserId.Values)
                {
                    con.Execute(() =>
                    {
                        con.Disconnect(string.Join(" ", text));
                    });

                    kicked++;
                }
                SiteLinkLogger.Info($"Kicked (f=green){kicked}(f=white) clients with reason (f=yellow){string.Join(" ", text)}(f=white)", "kick");
                return;

            default:
                if (!RemoteConnection.TryGet(userId, out RemoteConnection client))
                {
                    SiteLinkLogger.Info($"Not found player with userid (f=green){userId}(f=white)", "kick");
                    return;
                }

                client.Execute(() =>
                {
                    client.Disconnect(string.Join(" ", text));
                });

                SiteLinkLogger.Info($"Kicked (f=green){userId}(f=white) with reason (f=yellow){string.Join(" ", text)}(f=white)", "kick");
                break;
        }
    }
}
