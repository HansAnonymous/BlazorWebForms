using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class DemoFormFactory
{
    public static FormDefinition CreateDefaultDefinition() =>
        new()
        {
            Title = "Travel request",
            Description = "Route travel approvals through manager review and retain exact version snapshots.",
            Branding = new BrandingDefinition
            {
                AccentColor = "#0f766e",
                HeroText = "Builder + publish + submit + approve in one package."
            },
            Sections =
            [
                new FormSectionDefinition
                {
                    Title = "Request details",
                    Description = "Primary submission information.",
                    Fields =
                    [
                        new FormFieldDefinition
                        {
                            Id = "employeeName",
                            Label = "Employee name",
                            Required = true,
                            Searchable = true
                        },
                        new FormFieldDefinition
                        {
                            Id = "destination",
                            Label = "Destination",
                            Required = true,
                            Searchable = true
                        },
                        new FormFieldDefinition
                        {
                            Id = "travelDate",
                            Kind = FormFieldKind.Date,
                            Label = "Travel date",
                            Required = true
                        },
                        new FormFieldDefinition
                        {
                            Id = "needsHotel",
                            Kind = FormFieldKind.Radio,
                            Label = "Hotel needed?",
                            Options =
                            [
                                new FormFieldOption { Value = "yes", Label = "Yes" },
                                new FormFieldOption { Value = "no", Label = "No" }
                            ]
                        },
                        new FormFieldDefinition
                        {
                            Id = "hotelNotes",
                            Kind = FormFieldKind.TextArea,
                            Label = "Hotel notes",
                            VisibilityCondition = "needsHotel=yes"
                        }
                    ]
                },
                new FormSectionDefinition
                {
                    Title = "Approvals",
                    Description = "Capture signature and optional attachments.",
                    Fields =
                    [
                        new FormFieldDefinition
                        {
                            Id = "managerSignature",
                            Kind = FormFieldKind.Signature,
                            Label = "Manager signature",
                            Required = true
                        },
                        new FormFieldDefinition
                        {
                            Id = "receiptUpload",
                            Kind = FormFieldKind.File,
                            Label = "Upload estimate or receipt"
                        }
                    ]
                }
            ]
        };
}
