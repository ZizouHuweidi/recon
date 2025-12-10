using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Threading.Channels;

namespace recon.Mossad;

public class OtlpLogProcessor : BaseProcessor<LogRecord>
{
    private readonly Channel<string> _channel;
    private readonly ILogger<OtlpLogProcessor> _logger;

    public OtlpLogProcessor(Channel<string> channel, ILogger<OtlpLogProcessor> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    public override void OnEnd(LogRecord logRecord)
    {
        try
        {
            // Extract log information
            var logMessage = $"[{logRecord.Timestamp:yyyy-MM-dd HH:mm:ss}] " +
                           $"[{logRecord.LogLevel}] " +
                           $"{logRecord.CategoryName}: {logRecord.FormattedMessage ?? logRecord.Body?.ToString() ?? ""}";

            // Add attributes if present
            if (logRecord.Attributes != null)
            {
                var attrs = string.Join(", ", logRecord.Attributes.Select(kv => $"{kv.Key}={kv.Value}"));
                if (!string.IsNullOrEmpty(attrs))
                {
                    logMessage += $" | {attrs}";
                }
            }

            // Push to channel (non-blocking)
            _channel.Writer.TryWrite(logMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process log record");
        }
    }
}
