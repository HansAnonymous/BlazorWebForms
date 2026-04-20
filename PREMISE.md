# BlazorWebForms
A dynamic forms package for Blazor.

# Environment
- .NET 10
- Blazor Web, InteractiveServer

# Features
- Compatible with localdb sql server or Azure SQL Server
- Form versioning
    - Past forms are viewable with the form version it was filled out in
- Rich forms creator/editor/builder
- Create unlimited forms
- Allow forms to be edited after submission (optionally based on form creator)
- Permissions system. Either Authentication (RBAC or invite via emails)
    - Who can see submitted entries
- Publish to domain
- Email notifications for form managers
- Entries downloadable as PDF
- Custom logo and images for forms
- Responsive design
- WCAG 2.1 AA compliance
- File uploading
- Form submission searching
- Send results to self and view own submissions
- Validation options including REGEX
- Form sections
- Conditional form elements and sections
- Rich text editing
- Form signatures (Like adobe sign)
    - Forms can be configured to require signatures and approvers
    - Eg. A manager can use an existing form they want a report to fill out
- Internationalization

# Optional Compatibility
- MudBlazor

# Inspiration
- [BlazorJsonForm](https://github.com/Apollo3zehn/BlazorJsonForm/tree/main)
    - The dynamic and defined structure of using JSON, but we will be using SQL Server
- [Google Forms](https://docs.google.com/forms/)
    - The ability to create forms on the fly, share them, and view results