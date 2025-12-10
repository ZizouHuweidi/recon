using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Register ClickHouse connection
builder.Services.AddSingleton(_ => 
{
    var connectionString = "Host=localhost;Port=8123;Database=default;User=default;Password=";
    return new ClickHouseConnection(connectionString);
});

var app = builder.Build();

app.MapDefaultEndpoints();

// GET /logs - Query logs from ClickHouse
app.MapGet("/logs", async (ClickHouseConnection ch, int limit = 50, int offset = 0) =>
{
    try
    {
        await ch.OpenAsync();
        
        var query = $@"
            SELECT 
                Timestamp,
                TimestampTime,
                ServiceName,
                SeverityText,
                Body,
                TraceId,
                SpanId
            FROM otel_logs
            ORDER BY Timestamp DESC
            LIMIT {limit}
            OFFSET {offset}
        ";
        
        var command = ch.CreateCommand();
        command.CommandText = query;
        using var reader = await command.ExecuteReaderAsync();
        
        var logs = new List<object>();
        while (await reader.ReadAsync())
        {
            logs.Add(new
            {
                timestamp = reader.GetDateTime(0),
                timestampTime = reader.GetDateTime(1),
                serviceName = reader.GetString(2),
                severityText = reader.GetString(3),
                body = reader.GetString(4),
                traceId = reader.GetString(5),
                spanId = reader.GetString(6)
            });
        }
        
        return Results.Ok(logs);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to query logs: {ex.Message}");
    }
    finally
    {
        await ch.CloseAsync();
    }
});

// GET /traces - Query traces from ClickHouse
app.MapGet("/traces", async (ClickHouseConnection ch, int limit = 50, int offset = 0) =>
{
    try
    {
        await ch.OpenAsync();
        
        var query = $@"
            SELECT 
                Timestamp,
                TraceId,
                SpanId,
                ParentSpanId,
                ServiceName,
                SpanName,
                SpanKind,
                Duration,
                StatusCode,
                StatusMessage
            FROM otel_traces
            ORDER BY Timestamp DESC
            LIMIT {limit}
            OFFSET {offset}
        ";
        
        var command = ch.CreateCommand();
        command.CommandText = query;
        using var reader = await command.ExecuteReaderAsync();
        
        var traces = new List<object>();
        while (await reader.ReadAsync())
        {
            traces.Add(new
            {
                timestamp = reader.GetDateTime(0),
                traceId = reader.GetString(1),
                spanId = reader.GetString(2),
                parentSpanId = reader.GetString(3),
                serviceName = reader.GetString(4),
                spanName = reader.GetString(5),
                spanKind = reader.GetString(6),
                duration = reader.GetInt64(7),
                statusCode = reader.GetString(8),
                statusMessage = reader.GetString(9)
            });
        }
        
        return Results.Ok(traces);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to query traces: {ex.Message}");
    }
    finally
    {
        await ch.CloseAsync();
    }
});

// GET /metrics - Query metrics from ClickHouse
app.MapGet("/metrics", async (ClickHouseConnection ch, int limit = 50, int offset = 0, string type = "gauge") =>
{
    try
    {
        await ch.OpenAsync();
        
        var tableName = type.ToLower() switch
        {
            "histogram" => "otel_metrics_histogram",
            "sum" => "otel_metrics_sum",
            _ => "otel_metrics_gauge"
        };
        
        var query = $@"
            SELECT 
                TimeUnix,
                ServiceName,
                MetricName,
                Value
            FROM {tableName}
            ORDER BY TimeUnix DESC
            LIMIT {limit}
            OFFSET {offset}
        ";
        
        var command = ch.CreateCommand();
        command.CommandText = query;
        using var reader = await command.ExecuteReaderAsync();
        
        var metrics = new List<object>();
        while (await reader.ReadAsync())
        {
            metrics.Add(new
            {
                timeUnix = reader.GetInt64(0),
                serviceName = reader.GetString(1),
                metricName = reader.GetString(2),
                value = reader.GetDouble(3)
            });
        }
        
        return Results.Ok(metrics);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to query metrics: {ex.Message}");
    }
    finally
    {
        await ch.CloseAsync();
    }
});

// GET /stats - Get database stats
app.MapGet("/stats", async (ClickHouseConnection ch) =>
{
    try
    {
        await ch.OpenAsync();
        
        var stats = new
        {
            logs = await GetTableCount(ch, "otel_logs"),
            traces = await GetTableCount(ch, "otel_traces"),
            metrics_gauge = await GetTableCount(ch, "otel_metrics_gauge"),
            metrics_sum = await GetTableCount(ch, "otel_metrics_sum"),
            metrics_histogram = await GetTableCount(ch, "otel_metrics_histogram")
        };
        
        return Results.Ok(stats);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get stats: {ex.Message}");
    }
    finally
    {
        await ch.CloseAsync();
    }
});

async Task<long> GetTableCount(ClickHouseConnection ch, string tableName)
{
    var command = ch.CreateCommand();
    command.CommandText = $"SELECT count() FROM {tableName}";
    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt64(result);
}

app.Run();
