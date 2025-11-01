namespace SiteLink.API.Commands;

public class HelpCommand
{
    [ConsoleCommand("help")]
    public static void OnHelpCommand(string[] args)
    {
        string text = $"Available commands:";

        foreach (var command in CommandsManager.RegisteredCommands)
        {
            if (command.Key == "help") continue;

            text += $"\n - {command.Key}";
        }

        SiteLinkLogger.Info(text, "help");
    }
}
