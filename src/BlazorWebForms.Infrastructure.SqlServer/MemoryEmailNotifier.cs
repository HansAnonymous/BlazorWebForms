using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class MemoryEmailNotifier : IEmailNotifier
{
    public List<string> Messages { get; } = [];
    private readonly HashSet<string> sentIdempotencyKeys = new(StringComparer.Ordinal);

    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Notify managers for form '{form.Name}'.");
        return Task.CompletedTask;
    }

    public Task<string?> NotifyApproverAssignedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!sentIdempotencyKeys.Add($"assigned:{idempotencyKey}"))
        {
            return Task.FromResult<string?>(null);
        }

        Messages.Add($"Approver assigned notification for '{step.ApproverEmail}' on form '{form.Name}'. Key '{idempotencyKey}'.");
        return Task.FromResult<string?>(null);
    }

    public Task<string?> NotifyApproverReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!sentIdempotencyKeys.Add($"reminder:{idempotencyKey}"))
        {
            return Task.FromResult<string?>(null);
        }

        Messages.Add($"Approver reminder for '{step.ApproverEmail}' on form '{form.Name}'. Key '{idempotencyKey}'.");
        return Task.FromResult<string?>(null);
    }

    public Task NotifyEntryApprovedAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Entry approved notification for submitter '{entry.SubmittedByEmail}' on form '{form.Name}'.");
        return Task.CompletedTask;
    }

    public Task NotifyEntryRejectedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Entry rejected notification for submitter '{entry.SubmittedByEmail}' on form '{form.Name}'. Reason '{step.RejectionReason}'.");
        return Task.CompletedTask;
    }

    public Task NotifyInvitationCreatedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Invitation created for '{invitation.Email}' on form '{form.Name}' with role '{invitation.Role}'. Token '{invitation.Token}'.");
        return Task.CompletedTask;
    }

    public Task NotifyInvitationAcceptedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Invitation accepted by '{invitation.Email}' on form '{form.Name}'.");
        return Task.CompletedTask;
    }

    public Task NotifyInvitationRevokedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Invitation revoked for '{invitation.Email}' on form '{form.Name}'.");
        return Task.CompletedTask;
    }
}
