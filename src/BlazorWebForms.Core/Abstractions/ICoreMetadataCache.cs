using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Abstractions;

public interface ICoreMetadataCache
{
    bool TryGetPublishedForm(string slug, out PublishedFormViewModel viewModel);
    void SetPublishedForm(string slug, PublishedFormViewModel viewModel);
    bool TryGetDashboard(Guid userId, out DashboardViewModel dashboard);
    void SetDashboard(Guid userId, DashboardViewModel dashboard);
    void InvalidateForms();
}
