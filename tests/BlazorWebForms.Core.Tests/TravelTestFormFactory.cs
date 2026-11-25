namespace BlazorWebForms.Core.Tests;

using BlazorWebForms.Core.Models;

internal static class TravelTestFormFactory
{
    public static FormDefinition CreateDefaultDefinition() => new()
    {
        Title = "Travel request",
        Description = "Route travel approvals through manager review and retain exact version snapshots.",
        Branding = new BrandingDefinition { AccentColor = "#0f766e" },
        Sections =
        [
            new FormSectionDefinition
            {
                Title = "Request details",
                Fields =
                [
                    new FormFieldDefinition { Id = "employeeName", Label = "Employee name", Required = true, Searchable = true },
                    new FormFieldDefinition { Id = "destination", Label = "Destination", Required = true, Searchable = true },
                    new FormFieldDefinition { Id = "travelDate", Kind = FormFieldKind.Date, Label = "Travel date", Required = true },
                    new FormFieldDefinition
                    {
                        Id = "needsHotel", Kind = FormFieldKind.Radio, Label = "Hotel needed?",
                        Options = [new FormFieldOption { Value = "yes", Label = "Yes" }, new FormFieldOption { Value = "no", Label = "No" }]
                    },
                    new FormFieldDefinition { Id = "hotelNotes", Kind = FormFieldKind.TextArea, Label = "Hotel notes", VisibilityCondition = "needsHotel=yes" }
                ]
            },
            new FormSectionDefinition
            {
                Title = "Approvals",
                Fields =
                [
                    new FormFieldDefinition { Id = "managerSignature", Kind = FormFieldKind.Signature, Label = "Manager signature", Required = true },
                    new FormFieldDefinition { Id = "receiptUpload", Kind = FormFieldKind.File, Label = "Upload estimate or receipt" }
                ]
            }
        ]
    };
}
