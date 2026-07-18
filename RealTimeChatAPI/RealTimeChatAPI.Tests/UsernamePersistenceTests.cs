using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RealTimeChatAPI.Data;
using RealTimeChatAPI.Models;

namespace RealTimeChatAPI.Tests;

public class UsernamePersistenceTests
{
    [Fact]
    public void UserModel_DefinesBoundedUniqueUsername()
    {
        var options = new DbContextOptionsBuilder<RealTimeChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new RealTimeChatDbContext(options);

        var userType = dbContext.Model.FindEntityType(typeof(User));
        Assert.NotNull(userType);

        var username = userType.FindProperty(nameof(User.Username));
        Assert.NotNull(username);
        Assert.Equal(20, username.GetMaxLength());

        var uniqueIndex = userType.GetIndexes().Single(index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(User.Username));
        Assert.True(uniqueIndex.IsUnique);
    }
}
