using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Middlewares;
using NewRelic.Api.Agent;
using System.Diagnostics;

// ===== CRITICAL DIAGNOSTICS =====
Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       NEW RELIC AGENT DIAGNOSTIC - STARTUP CHECK           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// 1. Check environment variables
Console.WriteLine("[1️⃣  ENV VARS] Checking New Relic environment configuration...");
var envVars = new Dictionary<string, string>
{
    { "CORECLR_ENABLE_PROFILING", Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING") ?? "NOT SET" },
    { "CORECLR_PROFILER", Environment.GetEnvironmentVariable("CORECLR_PROFILER") ?? "NOT SET" },
    { "CORECLR_PROFILER_PATH", Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH") ?? "NOT SET" },
    { "CORECLR_NEWRELIC_HOME", Environment.GetEnvironmentVariable("CORECLR_NEWRELIC_HOME") ?? "NOT SET" },
    { "NEW_RELIC_HOME", Environment.GetEnvironmentVariable("NEW_RELIC_HOME") ?? "NOT SET" },
    { "NEW_RELIC_LICENSE_KEY", string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")) ? "NOT SET" : "✓ SET" },
    { "NEW_RELIC_APP_NAME", Environment.GetEnvironmentVariable("NEW_RELIC_APP_NAME") ?? "NOT SET" }
};

foreach (var (key, value) in envVars)
{
    Console.WriteLine($"   {key}: {value}");
}

// 2. Check if profiler file exists
Console.WriteLine("\n[2️⃣  FILES] Checking profiler binary file...");
var profilerPath = Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH");
if (!string.IsNullOrEmpty(profilerPath) && File.Exists(profilerPath))
{
    var fileInfo = new FileInfo(profilerPath);
    Console.WriteLine($"   ✓ FOUND: {profilerPath}");
    Console.WriteLine($"   Size: {fileInfo.Length} bytes");
}
else
{
    Console.WriteLine($"   ✗ NOT FOUND: {profilerPath}");
    var newrelicHome = Environment.GetEnvironmentVariable("CORECLR_NEWRELIC_HOME") ?? "/app/newrelic";
    Console.WriteLine($"   Checking files in {newrelicHome}:");
    if (Directory.Exists(newrelicHome))
    {
        var files = Directory.GetFiles(newrelicHome);
        foreach (var f in files)
        {
            Console.WriteLine($"      - {Path.GetFileName(f)}");
        }
    }
    else
    {
        Console.WriteLine($"      ✗ Directory does not exist!");
    }
}

// 3. Check if agent is loaded
Console.WriteLine("\n[3️⃣  AGENT] Testing New Relic Agent attachment...");
try
{
    var agent = NewRelic.Api.Agent.NewRelic.GetAgent();
    if (agent != null)
    {
        Console.WriteLine($"   ✓✓✓ AGENT ATTACHED ✓✓✓");
        Console.WriteLine($"   Agent Type: {agent.GetType().FullName}");
        
        // Try to record a test metric
        try
        {
            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Startup/DiagnosticTest", 1);
            Console.WriteLine($"   ✓ Test metric recorded successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Could not record test metric: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"   ✗✗✗ AGENT NOT ATTACHED ✗✗✗");
        Console.WriteLine($"   GetAgent() returned null - Profiler may not be loaded");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"   ✗ ERROR accessing agent: {ex.Message}");
    Console.WriteLine($"   {ex.StackTrace}");
}

// 4. Check process info
Console.WriteLine("\n[4️⃣  PROCESS] Current process information...");
var currentProcess = Process.GetCurrentProcess();
Console.WriteLine($"   Process ID: {currentProcess.Id}");
Console.WriteLine($"   Process Name: {currentProcess.ProcessName}");

Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                  END OF DIAGNOSTICS                        ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// Load environment variables from .env file if needed
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")))
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
}

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