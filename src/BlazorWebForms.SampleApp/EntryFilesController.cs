using BlazorWebForms.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorWebForms.SampleApp;

[ApiController]
[Route("entry-files")]
public sealed class EntryFilesController(
    FormsApplicationService formsService,
    EntryFileDownloadTokenService tokenService) : ControllerBase
{
    [Authorize(Policy = AuthPolicies.SelfViewAccess)]
    [HttpGet("{entryId:guid}/{fileId:guid}")]
    public async Task<IActionResult> Download(Guid entryId, Guid fileId, [FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (!tokenService.TryValidate(token ?? string.Empty, entryId, fileId))
        {
            return Forbid();
        }

        try
        {
            var file = await formsService.OpenEntryFileAsync(entryId, fileId, cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (InvalidOperationException)
        {
            return Forbid();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
