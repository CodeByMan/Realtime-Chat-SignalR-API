using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Helpers;
using RealTimeChatAPI.Models;
using RealTimeChatAPI.Services.Users;
using RealTimeChatAPI.Services.Users.Commands.LoginUser;
using RealTimeChatAPI.Services.Users.Commands.RegisterUser;
using RealTimeChatAPI.Services.Users.Commands.UpdateUser;

namespace RealTimeChatAPI.Tests;

public class UserCommandTests
{
    [Fact]
    public async Task Registration_HashesPasswordAndStoresNormalizedUsername()
    {
        var repository = new FakeUsersRepository();
        var mapper = CreateMapper();
        var handler = new RegisterUserCommandHandler(
            NullLogger<RegisterUserCommandHandler>.Instance,
            repository,
            mapper);

        await handler.Handle(new RegisterUserCommand
        {
            Name = "Alice",
            Username = "Alice1",
            Password = "password123"
        }, CancellationToken.None);

        var user = await repository.GetByUsernameAsync("alice1");
        Assert.NotNull(user);
        Assert.NotEqual("password123", user.HashedPassword);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify("password123", user.HashedPassword));
    }

    [Fact]
    public async Task Login_ReturnsAValidTokenForCorrectCredentials()
    {
        var repository = new FakeUsersRepository();
        await repository.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Username = "alice",
            HashedPassword = BCrypt.Net.BCrypt.EnhancedHashPassword("password123")
        });
        var handler = new LoginUserCommandHandler(
            NullLogger<LoginUserCommandHandler>.Instance,
            repository,
            new JwtHelper(CreateConfiguration()));

        var token = await handler.Handle(new LoginUserCommand
        {
            Username = "alice",
            Password = "password123"
        }, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Login_RejectsIncorrectCredentials()
    {
        var repository = new FakeUsersRepository();
        await repository.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Username = "alice",
            HashedPassword = BCrypt.Net.BCrypt.EnhancedHashPassword("password123")
        });
        var handler = new LoginUserCommandHandler(
            NullLogger<LoginUserCommandHandler>.Instance,
            repository,
            new JwtHelper(CreateConfiguration()));

        await Assert.ThrowsAsync<InvalidLoginException>(() => handler.Handle(new LoginUserCommand
        {
            Username = "alice",
            Password = "wrong-password"
        }, CancellationToken.None));
    }


    [Fact]
    public async Task ProfileUpdate_AcceptsCurrentUsernameAndNormalizesIt()
    {
        var repository = new FakeUsersRepository();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Username = "alice",
            HashedPassword = "hash"
        };
        await repository.Add(user);
        var handler = new UpdateUserCommandHandler(
            NullLogger<UpdateUserCommandHandler>.Instance,
            repository,
            new FakeUserContext(user),
            CreateMapper());

        await handler.Handle(new UpdateUserCommand
        {
            Username = "ALICE"
        }, CancellationToken.None);

        var updatedUser = await repository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("alice", updatedUser.Username);
    }

    [Fact]
    public async Task ProfileUpdate_RejectsUsernameOwnedByAnotherUser()
    {
        var repository = new FakeUsersRepository();
        var currentUser = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Username = "alice",
            HashedPassword = "hash"
        };
        await repository.Add(currentUser);
        await repository.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Bob",
            Username = "bob",
            HashedPassword = "hash"
        });
        var handler = new UpdateUserCommandHandler(
            NullLogger<UpdateUserCommandHandler>.Instance,
            repository,
            new FakeUserContext(currentUser),
            CreateMapper());

        await Assert.ThrowsAsync<UsernameAlreadyUsedException>(() => handler.Handle(
            new UpdateUserCommand { Username = "BOB" },
            CancellationToken.None));
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(config => config.AddProfile<MappingProfile>());
        return configuration.CreateMapper();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-signing-secret-that-is-at-least-32-bytes-long",
                ["Jwt:Issuer"] = "RealTimeChatAPI.Tests",
                ["Jwt:Audience"] = "RealTimeChatAPI.Tests.Client",
                ["Jwt:ExpirationInMinutes"] = "30"
            })
            .Build();

    private sealed class FakeUserContext(User user) : IUserContext
    {
        public CurrentUser CurrentUser() => new(user.Id, user.Username, user.Name);
    }

    private sealed class FakeUsersRepository : IUsersRepository
    {
        private readonly Dictionary<Guid, User> users = [];

        public Task Add(User user)
        {
            users[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task<User?> GetByUsernameAsync(string username) =>
            Task.FromResult(users.Values.FirstOrDefault(user =>
                string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<User?> GetByIdAsync(Guid id) =>
            Task.FromResult(users.GetValueOrDefault(id));

        public Task UpdateAsync(User user)
        {
            users[user.Id] = user;
            return Task.CompletedTask;
        }
    }
}
