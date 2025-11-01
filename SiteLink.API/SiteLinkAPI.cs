namespace SiteLink.API;

public class SiteLinkAPI
{
    public const string Version = "0.0.1";
    public const string GameVersion = "14.2.0";

    public static void Initialize(IServiceCollection collection)
    {
        CommandsManager.Initialize();

        PluginsManager.Initialize(collection);
    }
}
