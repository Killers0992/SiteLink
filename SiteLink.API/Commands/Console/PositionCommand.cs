using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Commands;

public class PositionCommand
{
    [ConsoleCommand("position")]
    public static void OnPositionCommand(string[] args)
    {
        if (args.Length < 1)
        {
            SiteLinkLogger.Info(TranslationManager.Command("position.usage"), "position");
            return;
        }

        string userId = args[0];

        if (!RemoteConnection.TryGet(userId, out RemoteConnection client))
        {
            SiteLinkLogger.Info(TranslationManager.Command(
                "player.not_found",
                new TranslationContext().With("user_id", userId)), "position");
            return;
        }

        SiteLinkLogger.Info(TranslationManager.Command(
            "position.result",
            TranslationContext.For(client.Session)
                .With("position", client.Session.Position)
                .With("horizontal", client.Session.HorizontalRotation)
                .With("vertical", client.Session.VerticalRotation)), "position");
    }
}
