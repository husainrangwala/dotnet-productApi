using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Middlewares;
using NewRelic.Api.Agent;

// Load environment variables from .env file
Console.WriteLine("[STARTUP] Loading environment variables...");
var env = Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY");
Console.WriteLine($"[STARTUP] Current NEW_RELIC_LICENSE_KEY: {(string.IsNullOrEmpty(env) ? "NOT SET" : "SET")}");
if (string.IsNullOrEmpty(env))
{
    Console.WriteLine("[STARTUP] License key not found in environment, checking .env file...");
    var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
    if (File.Exists(envPath))
    {
        Console.WriteLine($"[STARTUP] Found .env file at: {envPath}");
        foreach (var line in File.ReadLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                if (parts[0].Trim().Contains("NEWRELIC") || parts[0].Trim().Contains("NEW_RELIC"))
                {
                    Console.WriteLine($"[STARTUP] Loaded {parts[0].Trim()}");
                }
            }
        }
    }
    else
    {
        Console.WriteLine($"[STARTUP] .env file NOT found at: {envPath}");
    }
}
else
{
    Console.WriteLine("[STARTUP] License key found in environment variables");
}

// Verify license key is loaded
var licenseKey = Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY");
var appName = Environment.GetEnvironmentVariable("NEW_RELIC_APP_NAME") ?? "ProductApi";
var enableProfiling = Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING");
var profilerPath = Environment.GetEnvironmentVariable("CORECLR_PROFILER");
var profilerHome = Environment.GetEnvironmentVariable("CORECLR_NEWRELIC_HOME");
var newRelicHome = Environment.GetEnvironmentVariable("NEWRELIC_HOME");

Console.WriteLine("\n=== NEW RELIC CONFIGURATION ===");
Console.WriteLine($"License Key: {(!string.IsNullOrEmpty(licenseKey) ? "✓ LOADED" : "✗ NOT FOUND")}");
Console.WriteLine($"App Name: {appName}");
Console.WriteLine($"Profiling Enabled: {enableProfiling ?? "NOT SET"}");
Console.WriteLine($"Profiler GUID: {profilerPath ?? "NOT SET"}");
Console.WriteLine($"Profiler Home: {profilerHome ?? "NOT SET"}");
Console.WriteLine($"New Relic Home: {newRelicHome ?? "NOT SET"}");
Console.WriteLine("================================\n");

// ... (your existing .env loading code) ...

// ===== ADD THIS CHECK HERE =====
Console.WriteLine("\n[AGENT DIAGNOSTIC] === Testing New Relic Agent Connection ===");
try
{
    var agent = NewRelic.Api.Agent.NewRelic.GetAgent();
    if (agent != null)
    {
        Console.WriteLine($"[AGENT DIAGNOSTIC] ✓ Agent instance obtained. Type: {agent.GetType().FullName}");
        Console.WriteLine($"[AGENT DIAGNOSTIC] ✓ Agent is theoretically attached to process.");

        // Try a simple metric to test
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Diagnostic/AgentTest", 1.0f);
        Console.WriteLine($"[AGENT DIAGNOSTIC] ✓ Test metric 'Custom/Diagnostic/AgentTest' recorded.");
    }
    else
    {
        Console.WriteLine($"[AGENT DIAGNOSTIC] ✗ CRITICAL: GetAgent() returned NULL.");
        Console.WriteLine($"[AGENT DIAGNOSTIC] ✗ Profiler is NOT loaded. Check CORECLR_PROFILER env var.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[AGENT DIAGNOSTIC] ✗ EXCEPTION when accessing agent: {ex.Message}");
}
Console.WriteLine("[AGENT DIAGNOSTIC] ==========================================\n");
// ===== END OF ADDED CODE =====

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

Console.WriteLine("[STARTUP] Building application...");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SQLite
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlite("Data Source=products.db"));

var app = builder.Build();

// ===== ADD THIS CHECK HERE =====
Console.WriteLine("\n[APP DIAGNOSTIC] === Verifying Agent in Built Application ===");
try
{
    // Get logger for dependency injection context
    var diLogger = app.Services.GetRequiredService<ILogger<Program>>();
    diLogger.LogInformation("[APP DIAGNOSTIC] Application built. Checking agent in DI context...");

    var diAgent = NewRelic.Api.Agent.NewRelic.GetAgent();
    if (diAgent != null)
    {
        diLogger.LogInformation($"[APP DIAGNOSTIC] ✓ Agent available in DI context.");
        diLogger.LogInformation($"[APP DIAGNOSTIC] Current transaction state: {(diAgent.CurrentTransaction != null ? "Active transaction" : "No active transaction")}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[APP DIAGNOSTIC] ✗ Error in DI check: {ex.Message}");
}
Console.WriteLine("[APP DIAGNOSTIC] ===========================================\n");
// ===== END OF ADDED CODE =====

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("[STARTUP] Application built successfully");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    logger.LogInformation("[STARTUP] Running in Development mode");
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add New Relic Metrics Middleware (MUST be before UseRouting)
logger.LogInformation("[STARTUP] Adding New Relic Metrics Middleware");
app.UseMiddleware<NewRelicMetricsMiddleware>();

app.UseRouting();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Automatically create the database on startup
logger.LogInformation("[STARTUP] Ensuring database is created...");
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    db.Database.EnsureCreated();
    logger.LogInformation("[STARTUP] Database check completed");
}

logger.LogInformation("[STARTUP] Application started successfully - Ready to receive requests");
logger.LogInformation("[METRICS] Waiting for requests to record metrics...");

app.Run();