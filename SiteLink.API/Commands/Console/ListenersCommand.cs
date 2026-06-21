using System.Text;

namespace SiteLink.API.Commands;

public class ListenersCommand
{
    [ConsoleCommand("listeners")]
    public static void OnListenersCommand(string[] args)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(TranslationManager.Command("listeners.header"));

        foreach (Listener listener in Listener.List)
        {
            sb.AppendLine(TranslationManager.Command(
                "listeners.entry",
                new TranslationContext()
                    .With("address", listener.ListenAddress)
                    .With("port", listener.ListenPort)
                    .With("connections", listener.Connections.Count)
                    .With("listener", listener.Name)));
        }

        SiteLinkLogger.Info(sb.ToString(), "listeners");
    }
}
