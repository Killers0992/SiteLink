using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Commands;

public class PositionCommand
{
    [ConsoleCommand("position")]
    public static void OnPositionCommand(string[] args)
    {
        if (args.Length < 1)
        {
            SiteLinkLogger.Info("Syntax: position <userid>", "position");
            return;
        }

        string userId = args[0];

        if (!RemoteConnection.TryGet(userId, out RemoteConnection client))
        {
            SiteLinkLogger.Info($"Not found player with userid (f=green){userId}(f=white)", "position");
            return;
        }

        SiteLinkLogger.Info($"Current position for (f=green){userId}(f=white), pos (f=cyan){client.Session.Position}(f=white), rot horizontal (f=cyan){client.Session.HorizontalRotation}(f=white) vertical (f=cyan){client.Session.VerticalRotation}(f=white)", "position");
    }
}
