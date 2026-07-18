using RealTimeChatAPI.Data.Seeders;
using RealTimeChatAPI.Extensions;
using RealTimeChatAPI.Hubs;
using RealTimeChatAPI.Middlewares;
using Scalar.AspNetCore;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddServices(builder.Configuration);

    var app = builder.Build();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<Seeder>();
        await seeder.Seed();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseCors("AllowOrigins");

    app.UseHttpsRedirection();

    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
            context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
    });

    app.UseMiddleware<ErrorHandlingMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<ChatHub>("/ChatHub");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
