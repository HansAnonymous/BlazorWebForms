using System.Collections.Concurrent;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class DefaultAntiAbuseGuard(BlazorWebFormsSqlServerOptions options) : IAntiAbuseGuard
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> uploadEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> outboundEvents = new(StringComparer.OrdinalIgnoreCase);

    public Task CheckUploadAllowedAsync(UserProfile user, FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (options.MaxUploadsPerMinute <= 0)
        {
            return Task.CompletedTask;
        }

        var key = $"upload:{user.UserId}:{user.Email}";
        EnsureWithinRateLimit(uploadEvents, key, options.MaxUploadsPerMinute, TimeSpan.FromMinutes(1), "Upload quota exceeded.");
        return Task.CompletedTask;
    }

    public Task CheckOutboundNotificationAllowedAsync(string channel, string recipient, CancellationToken cancellationToken = default)
    {
        if (options.MaxNotificationsPerMinute <= 0)
        {
            return Task.CompletedTask;
        }

        var key = $"notify:{channel}:{recipient}";
        EnsureWithinRateLimit(outboundEvents, key, options.MaxNotificationsPerMinute, TimeSpan.FromMinutes(1), "Notification quota exceeded.");
        return Task.CompletedTask;
    }

    private static void EnsureWithinRateLimit(
        ConcurrentDictionary<string, Queue<DateTimeOffset>> store,
        string key,
        int limit,
        TimeSpan window,
        string message)
    {
        var now = DateTimeOffset.UtcNow;
        var queue = store.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= limit)
            {
                throw new InvalidOperationException(message);
            }

            queue.Enqueue(now);
        }
    }
}
