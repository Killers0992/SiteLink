using SiteLink.API.Misc;

namespace SiteLink.Services;

public class LoggingService : BackgroundService
{
    static void WriteLogToFile(object message)
    {
        if (!Directory.Exists("Logs"))
            Directory.CreateDirectory("Logs");

        File.AppendAllLines($"Logs/log_{SiteLinkLogger.SessionTime.ToString("dd_MM_yyyy_hh_mm_ss")}.txt", [message.ToString()] );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {

                while (SiteLinkLogger.NewLogEntry.Count != 0)
                {
                    if (SiteLinkLogger.NewLogEntry.TryDequeue(out string entry))
                    {
                        WriteLogToFile(entry.FormatAnsi(true));
                        Console.WriteLine(entry.FormatAnsi());
                    }
                }
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error(ex);
            }

            await Task.Delay(1000);
        }
    }
}
