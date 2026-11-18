namespace OneSake.Server.Match;

public sealed record QueueEntry(string ConnectionId, string DisplayName, string? GuestId);

public sealed record QueuePair(QueueEntry First, QueueEntry Second);

// Quick Match waiting list. Singleton, so every access is guarded by a lock.
public sealed class MatchmakingQueue
{
    private readonly object _sync = new();
    private readonly List<QueueEntry> _waitingPlayers = [];

    // Adds the player. If somebody else is already waiting, both are removed and returned as a pair.
    public QueuePair? Enqueue(QueueEntry entry)
    {
        lock (_sync)
        {
            // A connection can only be in the queue once (joining again just refreshes the entry).
            _waitingPlayers.RemoveAll(waiting => waiting.ConnectionId == entry.ConnectionId);

            // The same guest (e.g. a second browser tab) must not be matched against itself.
            var opponent = _waitingPlayers.FirstOrDefault(waiting =>
                entry.GuestId is null
                || waiting.GuestId is null
                || !string.Equals(waiting.GuestId, entry.GuestId, StringComparison.OrdinalIgnoreCase));

            if (opponent is null)
            {
                _waitingPlayers.Add(entry);
                return null;
            }

            _waitingPlayers.Remove(opponent);
            return new QueuePair(opponent, entry);
        }
    }

    public bool Remove(string connectionId)
    {
        lock (_sync)
        {
            return _waitingPlayers.RemoveAll(waiting => waiting.ConnectionId == connectionId) > 0;
        }
    }

    public bool Contains(string connectionId)
    {
        lock (_sync)
        {
            return _waitingPlayers.Any(waiting => waiting.ConnectionId == connectionId);
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _waitingPlayers.Count;
            }
        }
    }
}
