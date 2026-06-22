namespace BlazorWebForms.Core.Models;

public enum FormFieldKind
{
    Text,
    TextArea,
    Number,
    Select,
    Checkbox,
    Radio,
    Date,
    File,
    RichText,
    Signature,
    RepeatableList
}

public enum PrefillSourceKind
{
    None,
    Claim,
    Employee,
    FixedValue,
    Custom
}

public enum FormAccessMode
{
    Authenticated,
    Public
}

public enum FormPermissionRole
{
    Owner,
    Manager,
    Approver,
    Submitter,
    Viewer,
    SelfViewer,
    Admin
}

public enum EntryStatus
{
    Draft,
    Submitted,
    NeedsApproval,
    Approved,
    Rejected
}

public enum ApprovalStepStatus
{
    Pending,
    Approved,
    Rejected
}

public enum SubmissionEditMode
{
    ImmutableRevisions,
    OverwriteLatest
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
    Expired
}

public enum VisibilityJoinOperator
{
    And,
    Or
}

public enum VisibilityRuleOperator
{
    Equals,
    NotEquals,
    Contains,
    Empty
}

public enum ApprovalAuditAction
{
    StepApproved,
    StepRejected,
    Resubmitted,
    GraphApproverAssigned,
    GraphApproverReminder
}
