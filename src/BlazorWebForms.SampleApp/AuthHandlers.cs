using Microsoft.AspNetCore.Authorization;

namespace BlazorWebForms.SampleApp;

public sealed class RoleSetAuthorizationHandler : AuthorizationHandler<RoleSetRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleSetRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (requirement.Roles.Any(role => context.User.IsInRole(role)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class SelfOrManagerAuthorizationHandler : AuthorizationHandler<SelfOrManagerRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SelfOrManagerRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin") || context.User.IsInRole("Owner") || context.User.IsInRole("Manager"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Submitter") || context.User.IsInRole("SelfViewer"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
