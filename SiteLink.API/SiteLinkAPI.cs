namespace SiteLink.API;

public class SiteLinkAPI
{
    public const string Version = "0.0.2";
    public const string GameVersion = "14.2.1";

    public static void Initialize(IServiceCollection collection)
    {
        CommandsManager.Initialize();

        PluginsManager.Initialize(collection);
    }
}
