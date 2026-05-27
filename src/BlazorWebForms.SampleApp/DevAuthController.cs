using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BlazorWebForms.SampleApp;

[ApiController]
[Route("auth")]
public sealed class DevAuthController : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string? user = null, [FromForm] string? role = null, [FromForm] string? returnUrl = null)
    {
        var normalizedUser = string.IsNullOrWhiteSpace(user) ? "casey.manager" : user.Trim();
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? "Manager" : role.Trim();
        var email = normalizedUser.Contains('@') ? normalizedUser : $"{normalizedUser}@example.com";
        var displayName = normalizedUser.Contains('@') ? normalizedUser.Split('@')[0] : normalizedUser;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, DeterministicGuid(email).ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, normalizedRole)
        };

        if (!normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Submitter"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return LocalRedirect(ToLocalReturnUrl(returnUrl));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromForm] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect(ToLocalReturnUrl(returnUrl));
    }

    private string ToLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        return Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var guidBytes = hash.Take(16).ToArray();
        return new Guid(guidBytes);
    }
}
