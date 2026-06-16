using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using System.Collections.Concurrent;

namespace BlazorWebForms.Core.Services;

internal sealed class InMemoryCoreMetadataCache : ICoreMetadataCache
{
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
        publishedBySlug[slug] = (viewModel, DateTimeOffset.UtcNow.Add(PublishedTtl));
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
        dashboardByUser[userId] = (dashboard, DateTimeOffset.UtcNow.Add(DashboardTtl));
    }

    public void InvalidateForms()
    {
        publishedBySlug.Clear();
        dashboardByUser.Clear();
    }
}
