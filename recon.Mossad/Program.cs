
using System.Threading.Channels;
using StackExchange.Redis;
using recon.Mossad;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Register Redis
builder.AddRedisClient("cache");

// Register Channel for Producer-Consumer
builder.Services.AddSingleton(Channel.CreateUnbounded<string>());

// Register Worker
builder.Services.AddHostedService<TelemetryWorker>();

var app = builder.Build();

app.MapPost("/ingest", async (HttpRequest request, Channel<string> channel) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    
    // Push to Channel
    await channel.Writer.WriteAsync(body);
    
    return Results.Accepted();
});

app.MapGet("/data", async (IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    // Get last 50 items
    var items = await db.ListRangeAsync("telemetry", 0, 49);
    return Results.Ok(items.Select(x => x.ToString()));
});

app.MapDelete("/data", async (IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    await db.KeyDeleteAsync("telemetry");
    return Results.Ok("Cleared");
});

app.MapDefaultEndpoints();
app.Run();
