using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

internal sealed class ClaimFormPrefillProvider : IFormPrefillProvider
{
    public string ProviderKey => "claim";

    public bool CanResolve(FormFieldPrefillDefinition prefill) =>
        prefill.Source == PrefillSourceKind.Claim;

    public Task<string?> ResolveAsync(FormPrefillRequest request, CancellationToken cancellationToken = default)
    {
        var user = request.Requester;
        var value = request.Field.Prefill.Key.Trim().ToLowerInvariant() switch
        {
            "name" or "displayname" or "display_name" => user.DisplayName,
            "email" or "mail" => user.Email,
            "userid" or "user_id" or "sub" or "nameidentifier" => user.UserId.ToString(),
            "isauthenticated" or "is_authenticated" => user.IsAuthenticated.ToString(),
            "roles" or "role" => string.Join(",", user.Roles),
            _ => null
        };

        return Task.FromResult<string?>(value);
    }
}

internal sealed class EmployeeFormPrefillProvider(IEmployeePrefillProvider employeePrefillProvider) : IFormPrefillProvider
{
    public string ProviderKey => "employee";

    public bool CanResolve(FormFieldPrefillDefinition prefill) =>
        prefill.Source == PrefillSourceKind.Employee;

    public async Task<string?> ResolveAsync(FormPrefillRequest request, CancellationToken cancellationToken = default)
    {
        var employeeEmail = request.SubjectEmail ?? request.Requester.Email;
        var employeeData = await employeePrefillProvider.GetEmployeeDataAsync(request.Requester, employeeEmail, cancellationToken);
        return employeeData.TryGetValue(request.Field.Prefill.Key, out var value) ? value : null;
    }
}

internal sealed class FixedValueFormPrefillProvider : IFormPrefillProvider
{
    public string ProviderKey => "fixed";

    public bool CanResolve(FormFieldPrefillDefinition prefill) =>
        prefill.Source is PrefillSourceKind.None or PrefillSourceKind.FixedValue;

    public Task<string?> ResolveAsync(FormPrefillRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(request.Field.DefaultValue);
}
