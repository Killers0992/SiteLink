using System.Text;
using SiteLink.API.Handlers;

namespace SiteLink.API.Commands;

public class CentralCommand
{
    [ConsoleCommand("central")]
    public static void OnCentralCommand(string[] args)
    {
        if (args.Length < 2)
        {
            SiteLinkLogger.Info("Syntax: central <listenerName> <cmd>", "send");
            return;
        }

        if (!Listener.TryGet(args[0].ToLower(), out Listener listener))
        {
            SiteLinkLogger.Info($"Listener with name {args[0]} not exists! check \"listeners\" command", "central");
            return;
        }

        string[] rawCmd = args.Skip(1).ToArray();

        string cmd = rawCmd[0].ToLower();
        string[] cmdArgs = rawCmd.Skip(1).ToArray();

        ScpServerListHandler.ExecuteCentralCommand(listener, cmd, cmdArgs);
    }
}
