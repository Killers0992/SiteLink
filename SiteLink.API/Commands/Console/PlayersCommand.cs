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
            sb.AppendLine($" - Server (f=cyan){server.Name}(f=white) [ (f=green){server.Clients.Count}(f=white) ] ((f=darkcyan){server}(f=white))");
            sb.AppendLine($"  -> On Server  ");
            foreach (var player in server.Clients)
            {
                sb.AppendLine($"  [(f=green){player.NetworkIdentityId}(f=white)] (f=cyan){player.PreAuth.UserId}(f=white) connection time (f=darkcyan){player.Connectiontime.ToReadableString()}(f=white)");
            }
        }

        SiteLinkLogger.Info(sb.ToString(), "players");
    }
}
