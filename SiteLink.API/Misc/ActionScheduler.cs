namespace SiteLink.API.Misc;

/// <summary>
/// Manages scheduled timed actions in sync with the Client’s PollEvents().
/// </summary>
public class ActionScheduler
{
    private readonly Dictionary<string, TimedAction> _actions = new();
    private readonly Client _client;

    public ActionScheduler(Client client)
    {
        _client = client;
    }

    /// <summary>
    /// Adds a new timed or repeating action.
    /// </summary>
    public void Add(TimedAction action)
    {
        if (_actions.TryGetValue(action.Name, out var existing))
            existing.Cancel();

        _actions[action.Name] = action;
    }

    /// <summary>
    /// Removes (cancels) a named action.
    /// </summary>
    public bool Remove(string name)
    {
        if (_actions.Remove(name, out var act))
        {
            act.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Called every Client.PollEvents() to update timers.
    /// </summary>
    public void Update()
    {
        if (_actions.Count == 0)
            return;

        var now = DateTime.Now;
        var toRemove = new List<string>();

        var keys = new List<string>(_actions.Keys);

        foreach (var name in keys)
        {
            if (!_actions.TryGetValue(name, out var action))
                continue;

            if (action.IsCancelled)
            {
                toRemove.Add(name);
                continue;
            }

            if (now >= action.NextExecution)
            {
                action.Execute(_client);

                if (action.IsCancelled || !action.IsRepeating)
                    toRemove.Add(name);
            }
        }

        // Safely remove after iteration
        foreach (var name in toRemove)
            _actions.Remove(name);
    }

    public void Clear()
    {
        foreach (var a in _actions.Values)
            a.Cancel();
        _actions.Clear();
    }
}