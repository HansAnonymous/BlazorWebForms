using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class DemoFormFactory
{
    public static FormDefinition CreateDefaultDefinition() =>
        new()
        {
            Title = "",
            Description = "",
            Branding = new BrandingDefinition
            {
                AccentColor = "#0f766e",
                HeroText = ""
            },
            Sections =
            [
                new FormSectionDefinition
                {
                    Title = "",
                    Description = "",
                    Fields =
                    []
                }
            ]
        };
}
