using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using recon.Api.Data;
using recon.Api.Models;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry endpoint
var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT"] ?? "http://localhost:4317";
var otlpHeaders = builder.Configuration["OTLP_HEADERS"] ?? "";

// Configure Serilog with OpenTelemetry sink
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "recon-api"
        };
        
        // Add headers if provided
        if (!string.IsNullOrEmpty(otlpHeaders))
        {
            var headerPairs = otlpHeaders.Split(',');
            foreach (var pair in headerPairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    options.Headers.Add(parts[0].Trim(), parts[1].Trim());
                }
            }
        }
    })
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=recon;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ReconDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure OpenTelemetry for traces and metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("recon-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
            if (!string.IsNullOrEmpty(otlpHeaders))
                options.Headers = otlpHeaders;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
            if (!string.IsNullOrEmpty(otlpHeaders))
                options.Headers = otlpHeaders;
        }));

var app = builder.Build();

// Use Serilog for request logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Auto-migration
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ReconDbContext>();
    try
    {
        await context.Database.MigrateAsync();
        Log.Information("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Migration failed");
    }
}

app.MapGet("/", () => 
{
    Log.Information("Root endpoint accessed");
    return "Recon API is running";
});

app.MapGet("/health", () =>
{
    Log.Information("Health check endpoint accessed");
    return Results.Ok(new { status = "healthy" });
});

// User endpoints
var users = app.MapGroup("/users");

users.MapGet("/", async (ReconDbContext db) =>
{
    Log.Information("Fetching all users");
    return await db.Users.ToListAsync();
});

users.MapGet("/{id}", async (int id, ReconDbContext db) =>
{
    Log.Information("Fetching user with id {UserId}", id);
    return await db.Users.FindAsync(id) is User user ? Results.Ok(user) : Results.NotFound();
});

users.MapPost("/", async (User user, ReconDbContext db) =>
{
    Log.Information("Creating new user: {UserName}", user.Name);
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
});

users.MapPut("/{id}", async (int id, User input, ReconDbContext db) =>
{
    Log.Information("Updating user with id {UserId}", id);
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    user.Name = input.Name;
    user.Email = input.Email;
    user.Role = input.Role;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

users.MapDelete("/{id}", async (int id, ReconDbContext db) =>
{
    Log.Information("Deleting user with id {UserId}", id);
    if (await db.Users.FindAsync(id) is User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Results.Ok(user);
    }

    return Results.NotFound();
});

try
{
    Log.Information("Starting Recon API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
