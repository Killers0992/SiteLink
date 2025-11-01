using System.Text;

namespace SiteLink.API.Commands;

public class ListenersCommand
{
    [ConsoleCommand("listeners")]
    public static void OnListenersCommand(string[] args)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Listeners:");

        foreach (Listener listener in Listener.List)
        {
            sb.AppendLine($" - (f=cyan){listener.ListenAddress}:{listener.ListenPort}(f=white) [ (f=green){listener.ClientById.Count}(f=white) ] ((f=darkcyan){listener.Name}(f=white))");
        }

        SiteLinkLogger.Info(sb.ToString(), "listeners");
    }
}
