using Microsoft.AspNetCore.Authorization;

namespace BlazorWebForms.SampleApp;

public sealed class RoleSetRequirement(params string[] roles) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Roles { get; } = roles;
}

public sealed class SelfOrManagerRequirement : IAuthorizationRequirement
{
}
