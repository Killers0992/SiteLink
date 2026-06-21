using System.Text;

namespace SiteLink.API.Commands;

public class PlayersCommand
{
    [ConsoleCommand("players")]
    public static void OnPlayersCommand(string[] args)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(TranslationManager.Command("players.header"));

        foreach (var server in Server.List)
        {
            Session[] sessions = server.GetSessionsSnapshot();

            sb.AppendLine(TranslationManager.Command(
                "players.server",
                TranslationContext.For(server: server)
                    .With("count", sessions.Length)
                    .With("endpoint", server)));
            foreach (Session session in sessions)
            {
                sb.AppendLine(TranslationManager.Command(
                    "players.entry",
                    TranslationContext.For(session, server)
                        .With("duration", session.SessionTime.ToReadableString())));
            }
        }

        SiteLinkLogger.Info(sb.ToString(), "players");
    }
}
