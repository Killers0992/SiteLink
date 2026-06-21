using SiteLink.API;
using SiteLink.API.Packages;
using SiteLink.API.Attributes;
using SiteLink.Services;

namespace SiteLink.Commands;

public static class UpdateCommand
{
    [ConsoleCommand("version")]
    public static void OnVersion(string[] args)
    {
        SiteLinkLogger.Info(TranslationManager.Command(
            "update.version",
            new TranslationContext()
                .With("version", BuildInformation.VersionText)
                .With("api_version", SiteLinkAPI.ApiVersionText)
                .With("game_version", SiteLinkAPI.GameVersionText)), "version");
    }

    [ConsoleCommand("update")]
    public static void OnUpdate(string[] args)
    {
        try
        {
            PackageManifest latest = PackageManager.CheckCoreAsync(
                Version.Parse(BuildInformation.VersionText)).GetAwaiter().GetResult();

            if (latest == null)
            {
                SiteLinkLogger.Info(TranslationManager.Command("update.current"), "update");
                return;
            }

            SiteLinkLogger.Info(TranslationManager.Command(
                "update.installing",
                new TranslationContext()
                    .With("current", BuildInformation.VersionText)
                    .With("latest", latest.Version)), "update");

            PackageManager.ApplyCoreUpdateAsync(latest).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(TranslationManager.Command(
                "update.failed",
                new TranslationContext().With("error", ex.Message)), "update");
        }
    }
}
