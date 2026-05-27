using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class DemoCurrentUserContext : ICurrentUserContext
{
    public static readonly Guid DefaultUserId = Guid.Parse("7b3cf89b-a17f-4be4-8b22-cd6b4f8c2d63");

    public UserProfile GetCurrentUser() =>
        new()
        {
            UserId = DefaultUserId,
            IsAuthenticated = true,
            DisplayName = "Casey Manager",
            Email = "casey@example.com",
            Roles = [FormPermissionRole.Owner, FormPermissionRole.Manager]
        };
}
