using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RealTimeChatAPI.Data;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.Data.Seeders;
using RealTimeChatAPI.Helpers;
using RealTimeChatAPI.Hubs;
using RealTimeChatAPI.Middlewares;
using RealTimeChatAPI.Services.Users;
using System.Text;

namespace RealTimeChatAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = GetRequiredConfigurationValue(configuration, "Jwt:Secret");
        var jwtIssuer = GetRequiredConfigurationValue(configuration, "Jwt:Issuer");
        var jwtAudience = GetRequiredConfigurationValue(configuration, "Jwt:Audience");

        if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
            throw new InvalidOperationException("Jwt:Secret must contain at least 32 bytes.");

        if (configuration.GetValue<int>("Jwt:ExpirationInMinutes") <= 0)
            throw new InvalidOperationException("Jwt:ExpirationInMinutes must be greater than zero.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = JwtTokenResolver.GetSignalRAccessToken(context.HttpContext.Request);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddScoped<ErrorHandlingMiddleware>();
        services.AddControllers();
        services.AddOpenApi();

        services.AddCors(options =>
        {
            var origins = configuration.GetSection("Origins").Get<string[]>()
                ?? ["http://localhost:5173"];

            options.AddPolicy("AllowOrigins", policyBuilder =>
                policyBuilder.WithOrigins(origins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });

        // Application services
        var assembly = typeof(ServiceCollectionExtensions).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly).AddFluentValidationAutoValidation();
        services.AddAutoMapper(assembly);

        services.AddScoped<JwtHelper>();

        services.AddScoped<IUserContext, UserContext>();
        services.AddHttpContextAccessor();

        services.AddSingleton<UserConnectionManager>();
        services.AddSingleton<IUserIdProvider, AuthenticatedUserIdProvider>();
        services.AddSignalR();

        // Data (infrastructure) services
        var connectionString = configuration.GetConnectionString("RealTimeChatDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:RealTimeChatDb is required. Configure it with User Secrets or an environment variable.");

        services.AddDbContext<RealTimeChatDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IMessagesRepository, MessagesRepository>();

        services.AddScoped<Seeder>();
    }

    private static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{key} is required. Configure it with User Secrets or an environment variable.");

        return value;
    }
}
