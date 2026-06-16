using System.Collections.Concurrent;
using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class InMemoryOperationalTelemetry : IOperationalTelemetry
{
    private readonly ConcurrentDictionary<string, long> counters = new(StringComparer.OrdinalIgnoreCase);

    public void TrackUpload(string source, long bytes, bool success)
    {
        Increment($"upload:{source}:{(success ? "success" : "failure")}");
        if (success)
        {
            IncrementBy("upload:bytes", bytes);
        }
    }

    public void TrackPdfExport(string source, long bytes, bool success)
    {
        Increment($"pdf:{source}:{(success ? "success" : "failure")}");
        if (success)
        {
            IncrementBy("pdf:bytes", bytes);
        }
    }

    public void TrackEmailDelivery(string channel, bool success)
    {
        Increment($"email:{channel}:{(success ? "success" : "failure")}");
    }

    public void TrackFailure(string area, string operation, string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "unknown"
            : reason.Trim().ToLowerInvariant().Replace(' ', '-');
        Increment($"failure:{area}:{operation}:{normalizedReason}");
    }

    public IReadOnlyDictionary<string, long> Snapshot() => new Dictionary<string, long>(counters, StringComparer.OrdinalIgnoreCase);

    private void Increment(string key)
    {
        counters.AddOrUpdate(key, 1, (_, current) => current + 1);
    }

    private void IncrementBy(string key, long value)
    {
        counters.AddOrUpdate(key, value, (_, current) => current + value);
    }
}
