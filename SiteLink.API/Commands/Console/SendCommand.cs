using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API.Commands.Console;

public class SendCommand
{
    [ConsoleCommand("send")]
    public static void OnSendCommand(string[] args)
    {
        if (args.Length < 2)
        {
            SiteLinkLogger.Info("Syntax: send <all/player/server> <server>", "send");
            return;
        }

        string serverName = string.Join(" ", args.Skip(1));

        if (!Server.TryGetByName(serverName, out Server server))
        {
            SiteLinkLogger.Info($"Server to send to with name {args[1]} does not exist!", "send");
            return;
        }

        switch (true)
        {
            case true when args[0].ToLower() == "all":
                int sent = 0;
                foreach (Client client in Listener.ClientByUserId.Values)
                {
                    if (client.Server == server)
                        continue;

                    client.Connect(server);
                }

                SiteLinkLogger.Info($"Sent (f=green){sent}(f=white) clients to server (f=green){server.Name}(f=white)", "send");
                break;
            case true when args[0].ToLower().Contains('@'):
                if (!Client.TryGet(args[0], out Client targetPlayer))
                {
                    SiteLinkLogger.Info($"Client with userid {args[0]} does not exist!", "send");
                    break;
                }

                targetPlayer.Connect(server);
                break;
            case true when Server.TryGetByName(args[0], out Server serverFrom) && server != null:
                if (server == serverFrom)
                {
                    SiteLinkLogger.Info("You can't send the population of a server to the server they are already on!", "send");
                }

                int sentPopulation = 0;

                foreach (Client player in serverFrom.Clients)
                {
                    player.Connect(server);
                    sentPopulation++;
                }

                SiteLinkLogger.Info($"Sent (f=green){sentPopulation}(f=white) clients from {serverFrom.Name} to clients (f=green){server.Name}(f=white)", "send");
                break;
            default:
                SiteLinkLogger.Info("You must use a player id in format of ID@Steam, ID@discord or ID@northwood or the name of a server to send from", "send");
                break;
        }
    }
}
