using Microsoft.Extensions.Hosting;
using SiteLink.API.Commands;
using SiteLink.API.Misc;

namespace SiteLink.Services;

public class CommandsService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string line = Console.ReadLine();

            if (string.IsNullOrEmpty(line)) continue;

            string[] spLine = line.Split(' ');

            string commandName = spLine[0].ToLower();
            string[] args = spLine.Skip(1).ToArray();

            if (CommandsManager.RegisteredCommands.TryGetValue(commandName, out CommandDelegate cmd))
            {
                try
                {
                    cmd?.Invoke(args);
                }
                catch (Exception ex)
                {
                    SiteLinkLogger.Error($"Failed executing command {ex}", commandName);
                }
            }
            else
            {
                SiteLinkLogger.Info("Unknown command. Type 'help' to see a list of available commands.", commandName);
            }

            await Task.Delay(15);
        }
    }
}
