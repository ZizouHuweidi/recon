using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using recon.Api.Data;
using recon.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=recon;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ReconDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure OpenTelemetry
var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT"] ?? "http://localhost:4317";
var otlpHeaders = builder.Configuration["OTLP_HEADERS"] ?? "authorization=9c1f90dd-227a-4c86-a832-f7ed3b833bdf";

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
            options.Headers = otlpHeaders;
        }));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
        options.Protocol = OtlpExportProtocol.Grpc;
        options.Headers = otlpHeaders;
    });
});

var app = builder.Build();

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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration failed: {ex.Message}");
    }
}

app.MapGet("/", () => "Recon API is running");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// User endpoints
var users = app.MapGroup("/users");

users.MapGet("/", async (ReconDbContext db) => await db.Users.ToListAsync());

users.MapGet("/{id}", async (int id, ReconDbContext db) =>
    await db.Users.FindAsync(id) is User user ? Results.Ok(user) : Results.NotFound());

users.MapPost("/", async (User user, ReconDbContext db) =>
{
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
});

users.MapPut("/{id}", async (int id, User input, ReconDbContext db) =>
{
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
    if (await db.Users.FindAsync(id) is User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Results.Ok(user);
    }

    return Results.NotFound();
});

app.Run();
