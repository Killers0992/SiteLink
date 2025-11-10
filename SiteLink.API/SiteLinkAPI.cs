namespace SiteLink.API;

public class SiteLinkAPI
{
    static Version _gameVersion;

    public const string Version = "0.0.3";
    public const string GameVersion = "14.2.0";

    public static Version GameVersionParsed
    {
        get
        {
            if (_gameVersion == null)
            {
                System.Version.TryParse(GameVersion, out _gameVersion);
            }

            return _gameVersion;
        }
    }

    public static void Initialize(IServiceCollection collection)
    {
        CommandsManager.Initialize();

        PluginsManager.Initialize(collection);
    }
}
