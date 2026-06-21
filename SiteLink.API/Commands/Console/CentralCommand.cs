using SiteLink.API.Handlers;

namespace SiteLink.API.Commands;

public class CentralCommand
{
    [ConsoleCommand("central")]
    public static void OnCentralCommand(string[] args)
    {
        if (args.Length < 2)
        {
            SiteLinkLogger.Info(TranslationManager.Command("central.usage"), "central");
            return;
        }

        if (!Listener.TryGet(args[0].ToLower(), out Listener listener))
        {
            SiteLinkLogger.Info(TranslationManager.Command(
                "listener.not_found",
                new TranslationContext().With("listener", args[0])), "central");
            return;
        }

        string[] rawCmd = args.Skip(1).ToArray();

        string cmd = rawCmd[0].ToLower();
        string[] cmdArgs = rawCmd.Skip(1).ToArray();

        ScpServerListHandler.ExecuteCentralCommand(listener, cmd, cmdArgs);
    }
}
