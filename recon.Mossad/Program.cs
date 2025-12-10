
using System.Threading.Channels;
using StackExchange.Redis;
using recon.Mossad;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Register Redis
builder.AddRedisClient("cache");

// Register Channel for Producer-Consumer
var channel = Channel.CreateUnbounded<string>();
builder.Services.AddSingleton(channel);

// Register Worker
builder.Services.AddHostedService<TelemetryWorker>();

var app = builder.Build();



// OTLP HTTP endpoint for receiving logs from Alloy
app.MapPost("/v1/logs", async (HttpRequest request, Channel<string> channel) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();

        // Try to parse as JSON (OTLP JSON format)
        if (!string.IsNullOrEmpty(body))
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(body);
                // Extract log records from OTLP JSON format
                if (jsonDoc.RootElement.TryGetProperty("resourceLogs", out var resourceLogs))
                {
                    foreach (var resourceLog in resourceLogs.EnumerateArray())
                    {
                        if (resourceLog.TryGetProperty("scopeLogs", out var scopeLogs))
                        {
                            foreach (var scopeLog in scopeLogs.EnumerateArray())
                            {
                                if (scopeLog.TryGetProperty("logRecords", out var logRecords))
                                {
                                    foreach (var logRecord in logRecords.EnumerateArray())
                                    {
                                        // Extract log message
                                        var timestamp = logRecord.TryGetProperty("timeUnixNano", out var ts)
                                            ? DateTimeOffset.FromUnixTimeMilliseconds(ts.GetInt64() / 1_000_000).ToString("yyyy-MM-dd HH:mm:ss")
                                            : DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                                        var bodyValue = logRecord.TryGetProperty("body", out var bodyProp) && bodyProp.TryGetProperty("stringValue", out var strValue)
                                            ? strValue.GetString() ?? ""
                                            : "";

                                        var severityText = logRecord.TryGetProperty("severityText", out var sevText)
                                            ? sevText.GetString() ?? "INFO"
                                            : "INFO";

                                        var logMessage = $"[{timestamp}] [{severityText}] {bodyValue}";

                                        // Add attributes if present
                                        if (logRecord.TryGetProperty("attributes", out var attrs))
                                        {
                                            var attrList = new List<string>();
                                            foreach (var attr in attrs.EnumerateArray())
                                            {
                                                var key = attr.TryGetProperty("key", out var k) ? k.GetString() : "";
                                                var value = attr.TryGetProperty("value", out var v) && v.TryGetProperty("stringValue", out var sv)
                                                    ? sv.GetString() : "";
                                                if (!string.IsNullOrEmpty(key))
                                                    attrList.Add($"{key}={value}");
                                            }
                                            if (attrList.Any())
                                                logMessage += $" | {string.Join(", ", attrList)}";
                                        }

                                        // Push to channel
                                        await channel.Writer.WriteAsync(logMessage);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // If parsing fails, just store the raw body
                await channel.Writer.WriteAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] RAW: {body}");
            }
        }

        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to process logs: {ex.Message}");
    }
});

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
