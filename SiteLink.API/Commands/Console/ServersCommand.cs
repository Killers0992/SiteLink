using System.Text;

namespace SiteLink.API.Commands;

public class ServersCommand
{
    [ConsoleCommand("servers")]
    public static void OnServerCommand(string[] args)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(TranslationManager.Command("servers.header"));

        foreach (var server in Server.List)
        {
            sb.AppendLine(TranslationManager.Command(
                "servers.entry",
                TranslationContext.For(server: server)
                    .With("endpoint", server)));
        }

        SiteLinkLogger.Info(sb.ToString(), "servers");
    }
}
