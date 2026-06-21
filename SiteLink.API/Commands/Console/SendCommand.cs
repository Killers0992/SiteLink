using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Commands.Console;

public class SendCommand
{
    [ConsoleCommand("send")]
    public static void OnSendCommand(string[] args)
    {
        if (args.Length < 2)
        {
            SiteLinkLogger.Info(TranslationManager.Command("send.usage"), "send");
            return;
        }

        string serverName = string.Join(" ", args.Skip(1));

        if (!Server.TryGetByName(serverName, out Server server))
        {
            SiteLinkLogger.Info(TranslationManager.Command(
                "server.not_found",
                new TranslationContext().With("server_name", args[1])), "send");
            return;
        }

        switch (true)
        {
            case true when args[0].ToLower() == "all":
                int sent = 0;
                foreach (RemoteConnection client in RemoteConnection.ConnectionByUserId.Values)
                {
                    if (client.Session.Server == server)
                        continue;

                    client.Execute(() =>
                    {
                        client.Connect(server, true);
                    });
                    sent++;
                }

                SiteLinkLogger.Info(TranslationManager.Command(
                    "send.complete",
                    TranslationContext.For(server: server).With("count", sent)), "send");
                break;
            case true when args[0].ToLower().Contains('@'):
                if (!RemoteConnection.TryGet(args[0], out RemoteConnection targetPlayer))
                {
                    SiteLinkLogger.Info(TranslationManager.Command(
                        "player.not_found",
                        new TranslationContext().With("user_id", args[0])), "send");
                    break;
                }

                targetPlayer.Execute(() =>
                {
                    targetPlayer.Connect(server, true);
                });
                break;
            case true when Server.TryGetByName(args[0], out Server serverFrom) && server != null:
                if (server == serverFrom)
                {
                    SiteLinkLogger.Info(TranslationManager.Command("send.same_server"), "send");
                    break;
                }

                int sentPopulation = 0;

                foreach (Session session in serverFrom.GetSessionsSnapshot())
                {
                    session?.Connection.Execute(() =>
                    {
                        session?.Connection.Connect(server, true);
                    });
                    sentPopulation++;
                }

                SiteLinkLogger.Info(TranslationManager.Command(
                    "send.complete_from",
                    TranslationContext.For(server: server)
                        .With("source_server", serverFrom.Name)
                        .With("count", sentPopulation)), "send");
                break;
            default:
                SiteLinkLogger.Info(TranslationManager.Command("send.invalid_source"), "send");
                break;
        }
    }
}
