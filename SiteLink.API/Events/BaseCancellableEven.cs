namespace SiteLink.API.Events;

public class BaseCancellableEvent : BaseEvent
{
    public bool IsCancelled { get; set; }
}
