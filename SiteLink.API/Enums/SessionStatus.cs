namespace SiteLink.API.Enums;

public enum SessionStatus
{
    None,
    Connecting,
    Challenge,
    PreAuthentication,
    Connected,
    Retrying,
    Timeout,
}