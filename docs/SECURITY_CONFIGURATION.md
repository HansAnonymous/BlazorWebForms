# Security Configuration Guidance

## Secret Handling
Do not store production credentials in source-controlled configuration files.

Use secure providers for these values:
- `BlazorWebFormsSqlServerOptions.SmtpPassword`
- `BlazorWebFormsSqlServerOptions.SendGridApiKey`
- `BlazorWebFormsSqlServerOptions.GraphAccessToken`
- database connection strings

Recommended sources:
- environment variables,
- user secrets for local development,
- managed secret stores (for example Azure Key Vault) in hosted environments.

## Email Configuration Safety
- Keep `EmailProviderStrategy` as `DryRun` outside approved environments unless outbound email is explicitly required.
- When outbound email is enabled, set a valid `EmailFromAddress` in deployment configuration.
- Do not enable graph integration without both `GraphSender` and `GraphAccessToken` configured.

## Operational Practice
- Rotate API keys and SMTP credentials periodically.
- Restrict secret access by environment and role.
- Monitor failed email/database authentication events using centralized logs.
