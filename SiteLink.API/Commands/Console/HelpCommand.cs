namespace SiteLink.API.Commands;

public class HelpCommand
{
    [ConsoleCommand("help")]
    public static void OnHelpCommand(string[] args)
    {
        string text = TranslationManager.Command("help.header");

        foreach (var command in CommandsManager.RegisteredCommands)
        {
            if (command.Key == "help") continue;

            text += "\n" + TranslationManager.Command(
                "help.entry",
                new TranslationContext().With("command", command.Key));
        }

        SiteLinkLogger.Info(text, "help");
    }
}
