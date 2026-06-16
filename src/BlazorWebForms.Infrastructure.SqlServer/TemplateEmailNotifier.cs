using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class TemplateEmailNotifier(
    BlazorWebFormsSqlServerOptions options,
    IIntegrationGateway integrations,
    IAntiAbuseGuard antiAbuseGuard,
    IOperationalTelemetry telemetry) : IEmailNotifier
{
    private readonly HashSet<string> sentKeys = new(StringComparer.Ordinal);

    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.CompletedTask;
        }

        return SendWithRetryAsync($"submit:{entry.Id}",
            $"New submission: {form.Name}",
            $"A new entry was submitted to '{form.Name}'.", cancellationToken);
    }

    public Task<string?> NotifyApproverAssignedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.FromResult<string?>(null);
        }

        return SendWithRetryAsync($"assigned:{idempotencyKey}",
            $"Approval requested: {form.Name}",
            $"You are assigned to approve an entry for form '{form.Name}'.",
            async ct =>
            {
                if (!options.EnableGraphIntegration)
                {
                    return null;
                }

                var result = await integrations.Graph.SendApprovalReminderAsync(form, entry, step, ct);
                result.Data.TryGetValue("correlationId", out var correlationId);
                return correlationId;
            },
            cancellationToken);
    }

    public Task<string?> NotifyApproverReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.FromResult<string?>(null);
        }

        return SendWithRetryAsync($"reminder:{idempotencyKey}",
            $"Approval reminder: {form.Name}",
            $"Reminder: an entry is waiting for your approval.",
            async ct =>
            {
                if (options.EnableGraphIntegration)
                {
                    var result = await integrations.Graph.SendApprovalReminderAsync(form, entry, step, ct);
                    result.Data.TryGetValue("correlationId", out var correlationId);
                    return correlationId;
                }
                return null;
            },
            cancellationToken);
    }

    public Task NotifyEntryApprovedAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.CompletedTask;
        }

        return SendWithRetryAsync($"approved:{entry.Id}",
            $"Entry approved: {form.Name}",
            $"Your submission has been approved.", cancellationToken);
    }

    public Task NotifyEntryRejectedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.CompletedTask;
        }

        return SendWithRetryAsync($"rejected:{entry.Id}:{step.Id}",
            $"Entry rejected: {form.Name}",
            $"Your submission was rejected. Reason: {step.RejectionReason}", cancellationToken);
    }

    public Task NotifyInvitationCreatedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.CompletedTask;
        }

        return SendWithRetryAsync($"invite-created:{invitation.Id}",
            $"Invitation to {form.Name}",
            $"You have been invited as {invitation.Role}.",
            async ct =>
            {
                if (options.EnableGraphIntegration)
                {
                    var result = await integrations.Graph.SendInvitationAsync(form, invitation, ct);
                    result.Data.TryGetValue("correlationId", out var correlationId);
                    return correlationId;
                }
                return null;
            },
            cancellationToken);
    }

    public Task NotifyInvitationAcceptedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.CompletedTask;
        }

        return SendWithRetryAsync($"invite-accepted:{invitation.Id}",
            $"Invitation accepted: {form.Name}",
            $"Invitation for {invitation.Email} was accepted.", cancellationToken);
    }

    public Task NotifyInvitationRevokedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        if (!options.EnableOutboundEmail)
        {
            return Task.CompletedTask;
        }

        return SendWithRetryAsync($"invite-revoked:{invitation.Id}",
            $"Invitation revoked: {form.Name}",
            $"Invitation for {invitation.Email} was revoked.", cancellationToken);
    }

    private async Task<string?> SendWithRetryAsync(string key, string subject, string body, CancellationToken cancellationToken)
    {
        return await SendWithRetryAsync(key, subject, body, null, cancellationToken);
    }

    private async Task<string?> SendWithRetryAsync(
        string key,
        string subject,
        string body,
        Func<CancellationToken, Task<string?>>? afterSend,
        CancellationToken cancellationToken)
    {
        if (!sentKeys.Add(key))
        {
            return null;
        }

        Exception? last = null;
        for (var attempt = 1; attempt <= options.EmailRetryCount; attempt++)
        {
            try
            {
                await antiAbuseGuard.CheckOutboundNotificationAllowedAsync("email", "integration@example.com", cancellationToken);
                await integrations.Email.SendAsync("integration@example.com", subject, body, cancellationToken);
                telemetry.TrackEmailDelivery("email", success: true);
                if (afterSend is not null)
                {
                    return await afterSend(cancellationToken);
                }
                return null;
            }
            catch (Exception ex) when (attempt < options.EmailRetryCount)
            {
                last = ex;
                telemetry.TrackEmailDelivery("email", success: false);
                telemetry.TrackFailure("email", "send", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromMilliseconds(options.EmailRetryDelayMs), cancellationToken);
            }
            catch (Exception ex)
            {
                last = ex;
                telemetry.TrackEmailDelivery("email", success: false);
                telemetry.TrackFailure("email", "send", ex.GetType().Name);
                break;
            }
        }

        _ = last;
        return null;
    }
}
