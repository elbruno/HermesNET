using Hermes.Core.Memory;
using Hermes.Core.Profiles;
using Hermes.Core.Services;
using Hermes.Core.Telemetry;
using Hermes.Core.Configuration;
using Hermes.Host;
using Hermes.Host.Middleware;
using Hermes.Host.Providers;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddHermesSettings(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        HermesSettingsPaths.GetDefaultUserConfigPath());

var connectionString = builder.Configuration["Database:ConnectionString"] ?? "Data Source=hermes.db";

// ── Controllers + JSON ──────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── OpenAPI / Swagger ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hermes REST API",
        Version = "v1",
        Description = "Local AI assistant REST API — chat, sessions, profiles, and memory."
    });

    // Include XML doc comments when present
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
    foreach (var xml in xmlFiles)
        c.IncludeXmlComments(xml, includeControllerXmlComments: true);
});

// ── CORS (M2 baseline: localhost:3000) ────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(TelemetryProvider.GetActivitySource().Name)
            .AddAspNetCoreInstrumentation();
    });

// ── Domain services ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IProfileService>(_ => new ProfileService(connectionString));

builder.Services.AddSingleton<ISessionService>(sp =>
    new SessionService(connectionString, sp.GetRequiredService<IProfileService>()));

builder.Services.AddSingleton<IMemoryService>(new MemoryStore(connectionString));

builder.Services.AddSingleton<IChatClient>(sp =>
    new ChatClientFactory(sp.GetRequiredService<IConfiguration>()).CreateClient());

builder.Services.AddScoped<IHermesChatService, HermesChatService>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Initialize stores (idempotent) ───────────────────────────────────────────
await app.Services.GetRequiredService<IProfileService>().InitializeAsync();
await app.Services.GetRequiredService<ISessionService>().InitializeAsync();
await ((MemoryStore)app.Services.GetRequiredService<IMemoryService>()).InitializeAsync();

// ── Middleware pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors("LocalDev");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hermes API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapControllers();

app.Run();

// Expose for integration tests
public partial class Program { }
