using System.Threading.Channels;
using StackExchange.Redis;

namespace recon.Mossad;

public class TelemetryWorker : BackgroundService
{
    private readonly Channel<string> _channel;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TelemetryWorker> _logger;

    public TelemetryWorker(Channel<string> channel, IConnectionMultiplexer redis, ILogger<TelemetryWorker> logger)
    {
        _channel = channel;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Simulate processing
                _logger.LogInformation("Processing telemetry: {Message}", message);
                
                // Push to Redis List
                await db.ListRightPushAsync("telemetry", message);
                
                // Trim list to keep only last 100 items (demo)
                await db.ListTrimAsync("telemetry", -100, -1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry");
            }
        }
    }
}
