using System.Text;

namespace SiteLink.API.Commands;

public class PlayersCommand
{
    [ConsoleCommand("players")]
    public static void OnPlayersCommand(string[] args)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Players on servers:");

        foreach (var server in Server.List)
        {
            Session[] sessions = server.GetSessionsSnapshot();

            sb.AppendLine($" - Server (f=cyan){server.Name}(f=white) [ (f=green){sessions.Length}(f=white) ] ((f=darkcyan){server}(f=white))");
            sb.AppendLine($"  -> On Server  ");
            foreach (Session session in sessions)
            {
                sb.AppendLine($"  [(f=green){session.NetworkId}(f=white)] (f=cyan){session.UserId}(f=white) connection time (f=darkcyan){session.SessionTime.ToReadableString()}(f=white)");
            }
        }

        SiteLinkLogger.Info(sb.ToString(), "players");
    }
}
