using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public sealed class ApprovalStepGuardResult
{
    public required ApprovalStepRecord Step { get; init; }
    public required FormAggregate Form { get; init; }
    public required UserProfile User { get; init; }
}

public static class ApprovalStepGuard
{
    public static async Task<ApprovalStepGuardResult> ValidateAsync(
        EntryRecord entry,
        Guid stepId,
        IFormsRepository repository,
        ICurrentUserContext currentUserContext,
        IPermissionEvaluator permissionEvaluator,
        CancellationToken cancellationToken)
    {
        if (entry.Status != EntryStatus.NeedsApproval)
        {
            throw new InvalidOperationException("Entry is not in a review state.");
        }

        var pendingOrdered = entry.ApprovalSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .OrderBy(s => s.Order)
            .ToList();
        if (pendingOrdered.Count == 0)
        {
            throw new InvalidOperationException("No pending approval steps remain.");
        }

        var nextStepId = pendingOrdered[0].Id;
        if (nextStepId != stepId)
        {
            throw new InvalidOperationException("Only the next pending approval step can be acted on.");
        }

        var step = entry.ApprovalSteps.FirstOrDefault(candidate => candidate.Id == stepId)
                   ?? throw new InvalidOperationException("Approval step not found.");
        if (step.Status != ApprovalStepStatus.Pending)
        {
            throw new InvalidOperationException("Approval step is not pending.");
        }

        var user = currentUserContext.GetCurrentUser();
        var form = await repository.GetFormAsync(entry.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var isApprover = string.Equals(step.ApproverEmail, user.Email, StringComparison.OrdinalIgnoreCase);
        if (!(permissionEvaluator.CanManageForm(form, user) || isApprover || user.Roles.Contains(FormPermissionRole.Admin)))
        {
            throw new InvalidOperationException("Current user cannot act on this approval step.");
        }

        return new ApprovalStepGuardResult { Step = step, Form = form, User = user };
    }
}
