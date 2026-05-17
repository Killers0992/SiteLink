namespace SiteLink.API.Interfaces
{
    public interface IMirrorEndpoint
    {
        void Send(NetworkWriter writer);
    }
}
