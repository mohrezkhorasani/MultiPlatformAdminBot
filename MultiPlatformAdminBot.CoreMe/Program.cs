using MultiPlatformAdminBot.CoreMe;
using Microsoft.EntityFrameworkCore;
using MultiPlatformAdminBot.Persistence;
using MultiPlatformAdminBot.Infrastructure;
using MultiPlatformAdminBot.Infrastructure.Services;
using Application.Interfaces;
using Application.Interfaces.UseCases;
using Application.Interfaces.IInfrastructure;
using Application.UseCases;
using MultiPlatformAdminBot.CoreMe.Presenter;
using Platforms.Bale;
using Platforms.Eitaa;
using Platforms.Rubika;
using Platforms.Telegram;
using MultiPlatformAdminBot.CoreMe.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MultiPlatformAdminBot.CoreMe.Middleware;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

// Load environment variables from .env file before building configuration
MultiPlatformAdminBot.Common.Utils.DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// --- JWT: signing key comes from configuration (user-secrets / env var), never from source ---
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via .env file or the JWT__SIGNINGKEY environment variable.");
}
builder.Services.AddSingleton(jwtOptions);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Add services to the container.
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly)
                                      .RegisterServicesFromAssembly(typeof(Application.Interfaces.IAppDbContext).Assembly));

// DB Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured. Set it in your .env file or environment variables.");
}
builder.Services.AddDbContext<BotDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<BotDbContext>());

// One-time, idempotent seeding (migrations, admin rows, legacy password migration)
builder.Services.AddScoped<DbSeeder>();

// Presenters
builder.Services.AddScoped(typeof(ApiPresenter<>));

// HTTP Clients
builder.Services.Configure<TelegramProxyOptions>(builder.Configuration.GetSection(TelegramProxyOptions.SectionName));
builder.Services.AddHttpClient("TelegramProxyClient")
    .AddHttpMessageHandler<TelegramProxyHandler>();

builder.Services.AddHttpClient<BaleBotClient>();
builder.Services.AddHttpClient<BaleMediaDownloader>();
builder.Services.AddHttpClient<EitaaBotClient>();
builder.Services.AddHttpClient<RubikaBotClient>();

// Platform Clients
builder.Services.AddScoped<IBotPlatformClient, EitaaBotClient>();
builder.Services.AddScoped<IBotPlatformClient, TelegramBotClientAdapter>();
builder.Services.AddScoped<IBotPlatformClient, BaleBotClient>();
builder.Services.AddScoped<IBotPlatformClient, RubikaBotClient>();

// Media Downloaders
builder.Services.AddScoped<ISourceMediaDownloader, TelegramMediaDownloader>();
builder.Services.AddScoped<ISourceMediaDownloader, BaleMediaDownloader>();
builder.Services.AddScoped<TelegramProxyHandler>();

// Use Cases
builder.Services.AddScoped<IBroadcastMessageUseCase, BroadcastMessageUseCase>();
builder.Services.AddSingleton<IAlbumAggregatorService, AlbumAggregatorService>();
builder.Services.AddHostedService<MultiPlatformAdminBot.CoreMe.Services.FileCleanupBackgroundService>();

// Background broadcast pipeline: webhooks ack fast, heavy work runs here.
builder.Services.AddSingleton(
    System.Threading.Channels.Channel.CreateBounded<MultiPlatformAdminBot.CoreMe.Services.BroadcastJob>(
        new System.Threading.Channels.BoundedChannelOptions(1000)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        }));
builder.Services.AddHostedService<MultiPlatformAdminBot.CoreMe.Services.BroadcastWorkerService>();
builder.Services.AddHostedService<MultiPlatformAdminBot.CoreMe.Services.ScheduledBroadcastWorker>();

// Webhook sub-handlers (scoped; created fresh per webhook update)
builder.Services.AddScoped<MultiPlatformAdminBot.CoreMe.Services.Webhook.AdminRequestHandler>();
builder.Services.AddScoped<MultiPlatformAdminBot.CoreMe.Services.Webhook.PendingBroadcastHandler>();
builder.Services.AddScoped<MultiPlatformAdminBot.CoreMe.Services.Webhook.MenuStateHandler>();

var app = builder.Build();

// Alert channel for exceptions (chat id from configuration, empty = disabled)
var errorChatId = app.Configuration["Notify:ErrorChatId"] ?? "";
MultiPlatformAdminBot.Common.Statics.LogAction = message =>
{
    Task.Run(async () =>
    {
        if (string.IsNullOrWhiteSpace(errorChatId)) return;
        try
        {
            using var scope = app.Services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IEnumerable<Application.Interfaces.IInfrastructure.IBotPlatformClient>>()
                                  .FirstOrDefault(c => c.PlatformName == "Telegram");
            if (client != null)
            {
                await client.SendTextMessageAsync(errorChatId, message);
            }
        }
        catch { /* alerting must never crash the host */ }
    });
};

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DbSeeder>().Seed();
}

// CORS: only the configured origins (default: local dev origins)
var allowedOrigins = (app.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToList();
if (allowedOrigins.Count == 0)
{
    allowedOrigins.Add("http://localhost:5173");
    allowedOrigins.Add("http://localhost:3000");
}
app.UseCors(x => x.WithOrigins(allowedOrigins.ToArray()).AllowAnyMethod().AllowAnyHeader());

// Swagger UI is protected by Basic auth (credentials from configuration)
app.UseMiddleware<SwaggerBasicAuthMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Detailed readiness probe: pings the database and reports live counts.
app.MapGet("/health/ready", async (BotDbContext db) =>
{
    if (!db.Database.CanConnect())
        return Results.Problem("Database is not reachable.", statusCode: 503);

    int adminCount = 0;
    int channelCount = 0;
    try
    {
        adminCount = await db.AdminUserTBLs.CountAsync();
        channelCount = await db.AdminChannelTBLs.CountAsync();
    }
    catch (Exception ex)
    {
        return Results.Problem("Database query failed: " + ex.Message, statusCode: 503);
    }
    return Results.Ok(new { status = "healthy", database = "up", adminCount, channelCount, time = DateTime.UtcNow });
});

app.Run();
