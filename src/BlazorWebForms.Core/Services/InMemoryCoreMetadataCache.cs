using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using System.Collections.Concurrent;

namespace BlazorWebForms.Core.Services;

internal sealed class InMemoryCoreMetadataCache : ICoreMetadataCache
{
    private const int MaxPublishedEntries = 256;
    private const int MaxDashboardEntries = 512;
    private readonly ConcurrentDictionary<string, (PublishedFormViewModel Value, DateTimeOffset ExpiresUtc)> publishedBySlug = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, (DashboardViewModel Value, DateTimeOffset ExpiresUtc)> dashboardByUser = new();
    private static readonly TimeSpan PublishedTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DashboardTtl = TimeSpan.FromSeconds(20);

    public bool TryGetPublishedForm(string slug, out PublishedFormViewModel viewModel)
    {
        viewModel = default!;
        if (!publishedBySlug.TryGetValue(slug, out var item))
        {
            return false;
        }

        if (item.ExpiresUtc < DateTimeOffset.UtcNow)
        {
            publishedBySlug.TryRemove(slug, out _);
            return false;
        }

        viewModel = item.Value;
        return true;
    }

    public void SetPublishedForm(string slug, PublishedFormViewModel viewModel)
    {
        var now = DateTimeOffset.UtcNow;
        publishedBySlug[slug] = (viewModel, now.Add(PublishedTtl));
        PruneCache(publishedBySlug, now, MaxPublishedEntries);
    }

    public bool TryGetDashboard(Guid userId, out DashboardViewModel dashboard)
    {
        dashboard = default!;
        if (!dashboardByUser.TryGetValue(userId, out var item))
        {
            return false;
        }

        if (item.ExpiresUtc < DateTimeOffset.UtcNow)
        {
            dashboardByUser.TryRemove(userId, out _);
            return false;
        }

        dashboard = item.Value;
        return true;
    }

    public void SetDashboard(Guid userId, DashboardViewModel dashboard)
    {
        var now = DateTimeOffset.UtcNow;
        dashboardByUser[userId] = (dashboard, now.Add(DashboardTtl));
        PruneCache(dashboardByUser, now, MaxDashboardEntries);
    }

    public void InvalidateForms()
    {
        publishedBySlug.Clear();
        dashboardByUser.Clear();
    }

    private static void PruneCache<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue Value, DateTimeOffset ExpiresUtc)> cache,
        DateTimeOffset now,
        int maxEntries)
        where TKey : notnull
    {
        foreach (var pair in cache)
        {
            if (pair.Value.ExpiresUtc < now)
            {
                cache.TryRemove(pair.Key, out _);
            }
        }

        var overflow = cache.Count - maxEntries;
        if (overflow <= 0)
        {
            return;
        }

        foreach (var key in cache
                     .OrderBy(item => item.Value.ExpiresUtc)
                     .Take(overflow)
                     .Select(item => item.Key)
                     .ToList())
        {
            cache.TryRemove(key, out _);
        }
    }
}
