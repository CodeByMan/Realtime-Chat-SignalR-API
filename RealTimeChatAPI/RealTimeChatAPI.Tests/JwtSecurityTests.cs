using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RealTimeChatAPI.Helpers;
using RealTimeChatAPI.Models;
using System.Text;

namespace RealTimeChatAPI.Tests;

public class JwtSecurityTests
{
    private const string Secret = "test-signing-secret-that-is-at-least-32-bytes-long";

    [Theory]
    [InlineData("/ChatHub")]
    [InlineData("/ChatHub/negotiate")]
    [InlineData("/chathub")]
    public void QueryToken_IsAcceptedOnlyForHubPath(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString("?access_token=test-token");

        var token = JwtTokenResolver.GetSignalRAccessToken(context.Request);

        Assert.Equal("test-token", token);
    }

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/messages/00000000-0000-0000-0000-000000000001")]
    [InlineData("/ChatHubOther")]
    public void QueryToken_IsRejectedOutsideHubPath(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString("?access_token=test-token");

        Assert.Null(JwtTokenResolver.GetSignalRAccessToken(context.Request));
    }

    [Fact]
    public async Task GeneratedToken_ValidatesIssuerAudienceSignatureAndExpiration()
    {
        var configuration = CreateConfiguration();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Name = "Alice",
            HashedPassword = "not-used"
        };
        var token = new JwtHelper(configuration).GenerateToken(user);
        var handler = new JsonWebTokenHandler();

        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateIssuer = true,
            ValidIssuer = "RealTimeChatAPI.Tests",
            ValidateAudience = true,
            ValidAudience = "RealTimeChatAPI.Tests.Client",
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        });

        Assert.True(result.IsValid);
        Assert.Equal(user.Id.ToString(), result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Fact]
    public async Task GeneratedToken_IsRejectedForWrongAudience()
    {
        var token = new JwtHelper(CreateConfiguration()).GenerateToken(new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Name = "Alice",
            HashedPassword = "not-used"
        });
        var handler = new JsonWebTokenHandler();

        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateIssuer = true,
            ValidIssuer = "RealTimeChatAPI.Tests",
            ValidateAudience = true,
            ValidAudience = "Wrong.Audience",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        });

        Assert.False(result.IsValid);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = "RealTimeChatAPI.Tests",
                ["Jwt:Audience"] = "RealTimeChatAPI.Tests.Client",
                ["Jwt:ExpirationInMinutes"] = "30"
            })
            .Build();
}
