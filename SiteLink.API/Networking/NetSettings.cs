namespace SiteLink.API.Networking;

public static class NetSettings
{
    public const int UpdateTime = 15;
    public const byte ChannelsCount = 5;

    public const int SessionDisconnectTimeout = 5000;
    public const int ListenerDisconnectTimeout = 6000;

    public const int SessionReconnectDelay = 300;
    public const int ListenerReconnectDelay = 400;

    public const int SessionMaxConnectAttempts = 3;
    public const int ListenerMaxConnectAttempts = 2;
}