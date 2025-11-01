using LiteNetLib;

namespace SiteLink.Misc;

public class CustomNetLogger : INetLogger
{
    private const string _tag = "LiteNetLib";

    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        string text = string.Format(str, args);
        switch (level)
        {
            case NetLogLevel.Error:
                SiteLinkLogger.Error(text, _tag);
                break;

            case NetLogLevel.Trace:
            case NetLogLevel.Warning:
                SiteLinkLogger.Warn(text, _tag);
                break;

            case NetLogLevel.Info:
                SiteLinkLogger.Info(text, _tag);
                break;
        }
    }
}
