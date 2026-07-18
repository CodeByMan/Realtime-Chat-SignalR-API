namespace RealTimeChatAPI.Hubs;

public sealed class UserConnectionManager
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, HashSet<string>> connections = [];

    public void Add(Guid userId, string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (syncRoot)
        {
            if (!connections.TryGetValue(userId, out var userConnectionIds))
            {
                userConnectionIds = new HashSet<string>(StringComparer.Ordinal);
                connections[userId] = userConnectionIds;
            }

            userConnectionIds.Add(connectionId);
        }
    }

    public void Remove(Guid userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return;

        lock (syncRoot)
        {
            if (!connections.TryGetValue(userId, out var userConnectionIds))
                return;

            userConnectionIds.Remove(connectionId);
            if (userConnectionIds.Count == 0)
                connections.Remove(userId);
        }
    }

    public IReadOnlyCollection<string> GetConnections(Guid userId)
    {
        lock (syncRoot)
        {
            return connections.TryGetValue(userId, out var userConnectionIds)
                ? userConnectionIds.ToArray()
                : [];
        }
    }
}
