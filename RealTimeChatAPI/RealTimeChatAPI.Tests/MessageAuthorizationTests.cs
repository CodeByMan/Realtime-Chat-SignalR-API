using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealTimeChatAPI.Data;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.Helpers;
using RealTimeChatAPI.Models;
using RealTimeChatAPI.Services.Messages.Queries.GetMessages;
using RealTimeChatAPI.Services.Users;

namespace RealTimeChatAPI.Tests;

public class MessageAuthorizationTests
{
    [Fact]
    public async Task MessageHistory_ReturnsOnlyMessagesWhereCurrentUserIsAParticipant()
    {
        await using var dbContext = CreateDbContext();
        var currentUser = CreateUser("alice");
        var requestedUser = CreateUser("bob");
        var unrelatedUser = CreateUser("charlie");
        dbContext.Users.AddRange(currentUser, requestedUser, unrelatedUser);
        dbContext.Messages.AddRange(
            new Message
            {
                Id = Guid.NewGuid(),
                SenderId = currentUser.Id,
                RecipientId = requestedUser.Id,
                Content = "authorized"
            },
            new Message
            {
                Id = Guid.NewGuid(),
                SenderId = requestedUser.Id,
                RecipientId = unrelatedUser.Id,
                Content = "not-authorized"
            });
        await dbContext.SaveChangesAsync();

        var usersRepository = new UsersRepository(dbContext);
        var messagesRepository = new MessagesRepository(dbContext);
        var mapperConfiguration = new MapperConfiguration(config => config.AddProfile<MappingProfile>());
        var handler = new GetMessagesQueryHandler(
            NullLogger<GetMessagesQueryHandler>.Instance,
            messagesRepository,
            usersRepository,
            new FakeUserContext(currentUser),
            mapperConfiguration.CreateMapper());

        var messages = (await handler.Handle(
            new GetMessagesQuery { UserId = requestedUser.Id },
            CancellationToken.None)).ToList();

        Assert.Single(messages);
        Assert.Equal("authorized", messages[0].Content);
    }

    [Fact]
    public async Task OfflineMessage_IsPersistedAndCanBeRetrieved()
    {
        await using var dbContext = CreateDbContext();
        var sender = CreateUser("alice");
        var recipient = CreateUser("bob");
        dbContext.Users.AddRange(sender, recipient);
        await dbContext.SaveChangesAsync();
        var repository = new MessagesRepository(dbContext);

        await repository.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            SenderId = sender.Id,
            RecipientId = recipient.Id,
            Content = "stored while offline"
        });

        var messages = (await repository.GetMessagesAsync(sender.Id, recipient.Id)).ToList();
        Assert.Single(messages);
        Assert.Equal("stored while offline", messages[0].Content);
    }

    private static RealTimeChatDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RealTimeChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RealTimeChatDbContext(options);
    }

    private static User CreateUser(string username) => new()
    {
        Id = Guid.NewGuid(),
        Name = username,
        Username = username,
        HashedPassword = "not-used"
    };

    private sealed class FakeUserContext(User user) : IUserContext
    {
        public CurrentUser CurrentUser() => new(user.Id, user.Username, user.Name);
    }
}
