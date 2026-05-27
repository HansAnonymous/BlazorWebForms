using System.Security.Claims;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using Microsoft.AspNetCore.Http;

namespace BlazorWebForms.SampleApp;

internal sealed class ClaimsCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public UserProfile GetCurrentUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
        {
            return new UserProfile
            {
                UserId = Guid.Empty,
                IsAuthenticated = false,
                DisplayName = "Anonymous",
                Email = string.Empty,
                Roles = []
            };
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("oid")?.Value;

        var userId = Guid.TryParse(userIdClaim, out var parsed) ? parsed : DeterministicGuid(userIdClaim ?? principal.Identity.Name ?? "anonymous");

        var displayName = principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.Identity.Name
            ?? "Authenticated User";

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? string.Empty;

        var roles = ResolveRoles(principal);

        return new UserProfile
        {
            UserId = userId,
            IsAuthenticated = true,
            DisplayName = displayName,
            Email = email,
            Roles = roles
        };
    }

    private static List<FormPermissionRole> ResolveRoles(ClaimsPrincipal principal)
    {
        var roleClaims = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Concat(principal.FindAll("role").Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mapped = new List<FormPermissionRole>();
        foreach (var role in roleClaims)
        {
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add(FormPermissionRole.Admin);
            }
            else if (role.Equals("owner", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add(FormPermissionRole.Owner);
            }
            else if (role.Equals("manager", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add(FormPermissionRole.Manager);
            }
            else if (role.Equals("approver", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add(FormPermissionRole.Approver);
            }
            else if (role.Equals("viewer", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add(FormPermissionRole.Viewer);
            }
            else if (role.Equals("selfviewer", StringComparison.OrdinalIgnoreCase))
            {
                mapped.Add(FormPermissionRole.SelfViewer);
            }
        }

        if (!mapped.Contains(FormPermissionRole.Submitter))
        {
            mapped.Add(FormPermissionRole.Submitter);
        }

        return mapped.Distinct().ToList();
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var guidBytes = hash.Take(16).ToArray();
        return new Guid(guidBytes);
    }
}
