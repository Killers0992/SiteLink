using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Commands;

public class KickCommand
{
    [ConsoleCommand("kick")]
    public static void OnKickCommand(string[] args)
    {
        if (args.Length < 1)
        {
            SiteLinkLogger.Info(TranslationManager.Command("kick.usage"), "kick");
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
                SiteLinkLogger.Info(TranslationManager.Command(
                    "kick.complete",
                    new TranslationContext()
                        .With("count", kicked)
                        .With("reason", string.Join(" ", text))), "kick");
                return;

            default:
                if (!RemoteConnection.TryGet(userId, out RemoteConnection client))
                {
                    SiteLinkLogger.Info(TranslationManager.Command(
                        "player.not_found",
                        new TranslationContext().With("user_id", userId)), "kick");
                    return;
                }

                client.Execute(() =>
                {
                    client.Disconnect(string.Join(" ", text));
                });

                SiteLinkLogger.Info(TranslationManager.Command(
                    "kick.complete",
                    new TranslationContext()
                        .With("count", 1)
                        .With("reason", string.Join(" ", text))), "kick");
                break;
        }
    }
}
