namespace SiteLink.API.Models;

/// <summary>
/// Represents a timed or repeating scheduled action.
/// </summary>
public class TimedAction
{
    public string Name { get; }
    public TimeSpan Delay { get; }
    public TimeSpan? RepeatInterval { get; }
    public Action<Client> ActionToRun { get; }
    public TimedAction NextAction { get; }
    public DateTime NextExecution { get; set; }
    public bool IsRepeating => RepeatInterval.HasValue;
    public bool IsCancelled { get; private set; }

    public TimedAction(
        string name,
        TimeSpan delay,
        Action<Client> action,
        TimeSpan? repeatInterval = null,
        TimedAction nextAction = null)
    {
        Name = name ?? Guid.NewGuid().ToString();
        Delay = delay;
        RepeatInterval = repeatInterval;
        ActionToRun = action;
        NextAction = nextAction;
        NextExecution = DateTime.Now + delay;
    }

    public void Execute(Client client)
    {
        if (IsCancelled)
            return;

        try
        {
            ActionToRun?.Invoke(client);

            if (!IsRepeating && NextAction != null)
            {
                client.Scheduler.Add(NextAction);
            }

            if (IsRepeating)
            {
                NextExecution = DateTime.Now + RepeatInterval.Value;
            }
            else
            {
                IsCancelled = true;
            }
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error($"[TimedAction:{Name}] Exception: {ex}");
            IsCancelled = true;
        }
    }

    public void Cancel() => IsCancelled = true;
}