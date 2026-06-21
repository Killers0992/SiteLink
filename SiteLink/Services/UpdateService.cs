using SiteLink.API.Packages;
using SiteLink.API.Plugins;

namespace SiteLink.Services;

public sealed class UpdateService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!SiteLinkSettings.Singleton.CheckForUpdates)
            return;

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            PackageManifest core = await PackageManager.CheckCoreAsync(
                Version.Parse(BuildInformation.VersionText),
                stoppingToken);

            if (core != null)
            {
                SiteLinkLogger.Info(TranslationManager.Command(
                    "update.available",
                    new TranslationContext()
                        .With("current", BuildInformation.VersionText)
                        .With("latest", core.Version)), "Update");

                if (SiteLinkSettings.Singleton.AutoUpdate)
                {
                    await PackageManager.ApplyCoreUpdateAsync(core, stoppingToken);
                    return;
                }
            }

            foreach (Plugin plugin in PluginsManager.AssemblyToPlugin.Values)
            {
                PackageManifest latest =
                    await PackageManager.CheckPluginAsync(plugin, stoppingToken);
                if (latest == null)
                    continue;

                SiteLinkLogger.Info(TranslationManager.Command(
                    "plugins.update_available",
                    new TranslationContext()
                        .With("plugin", plugin.Name)
                        .With("current", plugin.Version)
                        .With("latest", latest.Version)), "Update");

                if (SiteLinkSettings.Singleton.AutoUpdate)
                    await PackageManager.InstallPluginAsync(plugin.Repository, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Warn(TranslationManager.Command(
                "update.check_failed",
                new TranslationContext().With("error", ex.Message)), "Update");
        }
    }
}
