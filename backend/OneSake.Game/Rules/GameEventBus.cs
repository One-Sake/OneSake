namespace OneSake.Game.Rules;

// Everything that happens in a match goes through here. Subscribers (the TriggerCollector)
// react to the events; the bus itself does not know any game rules.
public sealed class GameEventBus
{
    private const int HistorySize = 200;

    private readonly List<Action<GameEvent>> _handlers = [];
    private readonly List<GameEvent> _history = [];

    // The last events, newest last (for tests and debugging).
    public IReadOnlyList<GameEvent> History => _history;

    public void Subscribe(Action<GameEvent> handler)
    {
        _handlers.Add(handler);
    }

    public void Publish(GameEvent gameEvent)
    {
        _history.Add(gameEvent);
        if (_history.Count > HistorySize)
        {
            _history.RemoveAt(0);
        }

        foreach (var handler in _handlers)
        {
            handler(gameEvent);
        }
    }

    public void ClearHistory()
    {
        _history.Clear();
    }
}
