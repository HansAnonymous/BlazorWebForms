using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class GraphIntegration : IGraphIntegration
{
    public Task<GraphIntegrationResult> ExecuteAsync(string operation, IReadOnlyDictionary<string, string?> parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new InvalidOperationException("Graph operation is required.");
        }

        return Task.FromResult(new GraphIntegrationResult
        {
            Success = true,
            Message = $"Graph operation '{operation}' simulated.",
            Data = new Dictionary<string, string?>(parameters, StringComparer.OrdinalIgnoreCase)
            {
                ["correlationId"] = Guid.NewGuid().ToString("N")
            }
        });
    }

    public Task<GraphIntegrationResult> SendApprovalReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["formId"] = form.Id.ToString(),
            ["formName"] = form.Name,
            ["entryId"] = entry.Id.ToString(),
            ["approverEmail"] = step.ApproverEmail,
            ["approverName"] = step.ApproverName,
            ["stepId"] = step.Id.ToString(),
            ["stepOrder"] = step.Order.ToString()
        };

        return ExecuteAsync("approval-reminder", parameters, cancellationToken);
    }

    public Task<GraphIntegrationResult> SendInvitationAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["formId"] = form.Id.ToString(),
            ["formName"] = form.Name,
            ["invitationId"] = invitation.Id.ToString(),
            ["email"] = invitation.Email,
            ["role"] = invitation.Role.ToString(),
            ["scopeType"] = invitation.ScopeType,
            ["scopeValue"] = invitation.ScopeValue
        };

        return ExecuteAsync("invitation-send", parameters, cancellationToken);
    }
}
