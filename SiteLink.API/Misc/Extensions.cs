using SiteLink.API.Events;
using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using static SiteLink.API.Events.EventManager;

namespace SiteLink.API.Misc;

public static class Extensions
{
    private static string _version;
    private static string _parsedVersion;

    public const char ESC = (char)27;

    public static void RejectWithReason(
        this ConnectionRequest request,
        NetDataWriter writer,
        RejectionReason reason)
    {
        writer.Reset();
        writer.Put((byte)reason);
        request.RejectForce(writer);
    }

    public static string ParseVersion(this string version)
    {
        if (_version != version)
        {
            switch (version.ToUpper())
            {
                case "LATEST":
                    _parsedVersion = SiteLinkAPI.GameVersionText;
                    break;
                default:
                    _parsedVersion = version;
                    break;
            }
            _version = version;
        }

        return _parsedVersion;
    }

    public static bool ValidateGameVersion(this Version gameVersion, Version clientVersion, bool backwardsCompatible, byte backwardsRevision)
    {
        if (gameVersion.Major != clientVersion.Major || gameVersion.Minor != clientVersion.Minor)
            return false;

        if (backwardsCompatible)
            return gameVersion.Build == backwardsRevision;

        return gameVersion.Build >= backwardsRevision && gameVersion.Build <= clientVersion.Build;
    }

    public static void RejectWithMessage(this ConnectionRequest request, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            request.Reject();
            return;
        }

        NetDataWriter writer = new NetDataWriter();

        writer.Put((byte)RejectionReason.Custom);
        writer.Put(message);

        request.Reject(writer);
    }

    public static string ToReadableString(this TimeSpan span)
    {
        var duration = span.Duration();
        var sb = new StringBuilder(64);

        if (duration.Days > 0)
            sb.AppendFormat("{0:0} day{1}, ", duration.Days, duration.Days == 1 ? string.Empty : "s");

        if (duration.Hours > 0)
            sb.AppendFormat("{0:0} hour{1}, ", duration.Hours, duration.Hours == 1 ? string.Empty : "s");

        if (duration.Minutes > 0)
            sb.AppendFormat("{0:0} minute{1}, ", duration.Minutes, duration.Minutes == 1 ? string.Empty : "s");

        if (duration.Seconds > 0)
            sb.AppendFormat("{0:0} second{1}", duration.Seconds, duration.Seconds == 1 ? string.Empty : "s");

        string formatted = sb.ToString();

        if (formatted.EndsWith(", "))
            formatted = formatted.Substring(0, formatted.Length - 2);

        if (string.IsNullOrEmpty(formatted))
            formatted = "0 seconds";

        return formatted;
    }

    public static string FormatAnsi(this object message, bool forceRemove = false)
    {
        string text = message.ToString();

        return Regex.Replace(text, @"\(f=(.*?)\)", ev =>
        {
            if (SiteLinkLogger.AnsiDisabled || forceRemove)
                return string.Empty;

            string color = ev.Groups[1].Value.ToLower();

            switch (color)
            {
                case "black":
                    return $"{ESC}[30m";

                case "darkred":
                    return $"{ESC}[31m";
                case "darkgreen":
                    return $"{ESC}[32m";
                case "darkyellow":
                    return $"{ESC}[33m";
                case "darkblue":
                    return $"{ESC}[34m";
                case "darkmagenta":
                    return $"{ESC}[35m";
                case "darkcyan":
                    return $"{ESC}[36m";
                case "darkgray":
                    return $"{ESC}[90m";

                case "gray":
                    return $"{ESC}[37m";
                case "red":
                    return $"{ESC}[91m";
                case "green":
                    return $"{ESC}[92m";
                case "yellow":
                    return $"{ESC}[93m";
                case "blue":
                    return $"{ESC}[94m";
                case "magenta":
                    return $"{ESC}[95m";
                case "cyan":
                    return $"{ESC}[96m";

                case "white":
                    return $"{ESC}[97m";

                default:
                    return $"{ESC}[39m";
            }
        });
    }

    public static string Base64Decode(this string base64EncodedData)
    {
        byte[] bytes = Convert.FromBase64String(base64EncodedData);

        return Encoding.UTF8.GetString(bytes);
    }

    public static string Base64Encode(this string text)
    {
        byte[] array = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(text.Length));

        int bytes = Encoding.UTF8.GetBytes(text, 0, text.Length, array, 0);

        string result = Convert.ToBase64String(array, 0, bytes);

        ArrayPool<byte>.Shared.Return(array, false);

        return result;
    }


    public static void InvokeWithExceptionHandler<TEvent>(this CustomEventHandler<TEvent> ev, TEvent arguments) where TEvent : BaseEvent
    {
        foreach (var invoker in ev.GetInvocationList())
        {
            if (invoker is not CustomEventHandler<TEvent> customInvoker) continue;

            try
            {
                customInvoker.Invoke(arguments);
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Exception while invoking event (f=green){ev.GetType().Name}(f=red) {ex}", "EventManager");
            }
        }
    }

    public static Quaternion EulerToQuaternion(this VectorInfo vec)
    {
        // Convert degrees to radians
        float radX = vec.X * (float)Math.PI / 180f;
        float radY = vec.Y * (float)Math.PI / 180f;
        float radZ = vec.Z * (float)Math.PI / 180f;

        // Unity uses ZXY order
        float cX = (float)Math.Cos(radX * 0.5f);
        float sX = (float)Math.Sin(radX * 0.5f);
        float cY = (float)Math.Cos(radY * 0.5f);
        float sY = (float)Math.Sin(radY * 0.5f);
        float cZ = (float)Math.Cos(radZ * 0.5f);
        float sZ = (float)Math.Sin(radZ * 0.5f);

        Quaternion q;

        q.x = sX * cY * cZ + cX * sY * sZ;
        q.y = cX * sY * cZ - sX * cY * sZ;
        q.z = cX * cY * sZ - sX * sY * cZ;
        q.w = cX * cY * cZ + sX * sY * sZ;

        return q;
    }

    public static void ByteToBools(this byte b, out bool bool1, out bool bool2, out bool bool3, out bool bool4, out bool bool5, out bool bool6, out bool bool7, out bool bool8)
    {
        bool1 = ((b & 1) == 1);
        bool2 = ((b & 2) == 2);
        bool3 = ((b & 4) == 4);
        bool4 = ((b & 8) == 8);
        bool5 = ((b & 16) == 16);
        bool6 = ((b & 32) == 32);
        bool7 = ((b & 64) == 64);
        bool8 = ((b & 128) == 128);
    }

    public static byte BoolsToByte(bool bool1, bool bool2, bool bool3, bool bool4, bool bool5, bool bool6, bool bool7, bool bool8)
    {
        byte value = 0;

        if (bool1) value |= 1;
        if (bool2) value |= 2;
        if (bool3) value |= 4;
        if (bool4) value |= 8;
        if (bool5) value |= 16;
        if (bool6) value |= 32;
        if (bool7) value |= 64;
        if (bool8) value |= 128;

        return value;
    }
}
