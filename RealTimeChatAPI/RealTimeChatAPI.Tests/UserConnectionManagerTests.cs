using RealTimeChatAPI.Hubs;

namespace RealTimeChatAPI.Tests;

public class UserConnectionManagerTests
{
    [Fact]
    public void Add_SupportsMultipleConnectionsAndRejectsDuplicates()
    {
        var manager = new UserConnectionManager();
        var userId = Guid.NewGuid();

        manager.Add(userId, "connection-1");
        manager.Add(userId, "connection-2");
        manager.Add(userId, "connection-1");

        Assert.Equal(2, manager.GetConnections(userId).Count);
        Assert.Contains("connection-1", manager.GetConnections(userId));
        Assert.Contains("connection-2", manager.GetConnections(userId));
    }

    [Fact]
    public void Remove_OnlyRemovesTheDisconnectedConnection()
    {
        var manager = new UserConnectionManager();
        var userId = Guid.NewGuid();
        manager.Add(userId, "connection-1");
        manager.Add(userId, "connection-2");

        manager.Remove(userId, "connection-1");

        Assert.Equal(new[] { "connection-2" }, manager.GetConnections(userId));
    }

    [Fact]
    public void Remove_RemovesUserAfterLastConnectionCloses()
    {
        var manager = new UserConnectionManager();
        var userId = Guid.NewGuid();
        manager.Add(userId, "connection-1");

        manager.Remove(userId, "connection-1");

        Assert.Empty(manager.GetConnections(userId));
    }

    [Fact]
    public async Task ConcurrentAddsAndRemoves_DoNotLoseRemainingConnections()
    {
        var manager = new UserConnectionManager();
        var userId = Guid.NewGuid();
        var connectionIds = Enumerable.Range(0, 100)
            .Select(index => $"connection-{index}")
            .ToArray();

        await Task.WhenAll(connectionIds.Select(id => Task.Run(() => manager.Add(userId, id))));
        await Task.WhenAll(connectionIds.Take(50).Select(id => Task.Run(() => manager.Remove(userId, id))));

        Assert.Equal(50, manager.GetConnections(userId).Count);
    }
}
