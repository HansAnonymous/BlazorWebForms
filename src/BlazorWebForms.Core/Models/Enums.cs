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
    Signature
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
    SelfViewer
}

public enum EntryStatus
{
    Draft,
    Submitted,
    NeedsApproval,
    Approved
}

public enum ApprovalStepStatus
{
    Pending,
    Approved
}
