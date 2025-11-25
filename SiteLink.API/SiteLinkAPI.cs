using SiteLink.API;

[assembly: AssemblyProduct("SiteLinkAPI")]
[assembly: AssemblyCopyright("Killers0992 @ 2025")]
[assembly: AssemblyVersion(SiteLinkAPI.ApiVersionText)]

namespace SiteLink.API;

public class SiteLinkAPI
{
    static Version _gameVersion;
    static Version _apiVersion;

    public const string GameVersionText = "14.2.2";
    public const string ApiVersionText = "1.0.1";

    public static Version GameVersion
    {
        get
        {
            if (_gameVersion == null)
            {
                System.Version.TryParse(GameVersionText, out _gameVersion);
            }

            return _gameVersion;
        }
    }

    public static Version ApiVersion
    {
        get
        {
            if (_apiVersion == null)
            {
                System.Version.TryParse(ApiVersionText, out _apiVersion);
            }

            return _apiVersion;
        }
    }

    public static void Initialize(IServiceCollection collection)
    {
        SiteLinkLogger.Info($"Initializing proxy... ( Api Version (f=cyan){ApiVersionText}(f=white), Supported Game Version (f=cyan){GameVersionText}(f=white) )");

        CommandsManager.Initialize();

        PluginsManager.Initialize(collection);
    }
}
