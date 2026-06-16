using BlazorWebForms.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorWebForms.SampleApp;

[ApiController]
[Route("entry-pdf")]
public sealed class EntryPdfController(FormsApplicationService formsService) : ControllerBase
{
    [Authorize(Policy = AuthPolicies.SelfViewAccess)]
    [HttpGet("{entryId:guid}")]
    public async Task<IActionResult> Download(Guid entryId, CancellationToken cancellationToken)
    {
        try
        {
            var exported = await formsService.ExportEntryPdfAsync(entryId, cancellationToken);
            return File(exported.Content, exported.ContentType, exported.FileName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cannot export", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
