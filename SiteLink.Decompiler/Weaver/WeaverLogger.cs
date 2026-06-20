using Mono.CecilX;

namespace SiteLink.Decompiler.Weaver
{
    public interface Logger
    {
        void Warning(string message);
        void Warning(string message, MemberReference mr);
        void Error(string message);
        void Error(string message, MemberReference mr);
    }
}
