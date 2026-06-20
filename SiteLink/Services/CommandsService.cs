using SiteLink.API.Commands;
using SiteLink.API.Metrics;
using System.Diagnostics;

namespace SiteLink.Services;

public class CommandsService : BackgroundService
{
    public static CommandsService Instance { get; private set; }

    public ServiceStats Stats { get; } = new ServiceStats("CommandsService");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Instance = this;

        CommandsManager.RegisterConsoleCommandsInAssembly(typeof(Program).Assembly);

        while (!stoppingToken.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();

            string line = Console.ReadLine();

            if (string.IsNullOrEmpty(line))
            {
                stopwatch.Stop();
                Stats.RecordIteration(stopwatch.Elapsed);
                await Task.Delay(15);
                continue;
            }

            string[] spLine = line.Split(' ');

            string commandName = spLine[0].ToLower();
            string[] args = spLine.Skip(1).ToArray();

            if (CommandsManager.RegisteredCommands.TryGetValue(commandName, out CommandDelegate cmd))
            {
                try
                {
                    cmd?.Invoke(args);
                    Stats.RecordProcessedItem();
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

            stopwatch.Stop();
            Stats.RecordIteration(stopwatch.Elapsed);

            await Task.Delay(15);
        }
    }
}
