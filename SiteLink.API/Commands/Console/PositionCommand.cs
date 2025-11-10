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

        if (!Client.TryGet(userId, out Client client))
        {
            SiteLinkLogger.Info($"Not found player with userid (f=green){userId}(f=white)", "position");
            return;
        }

        SiteLinkLogger.Info($"Current position for (f=green){userId}(f=white), pos (f=cyan){client.Position}(f=white), rot horizontal (f=cyan){client.HorizontalRotation}(f=white) vertical (f=cyan){client.VerticalRotation}(f=white)", "position");
    }
}
