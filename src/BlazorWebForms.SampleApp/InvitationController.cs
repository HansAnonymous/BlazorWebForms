using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorWebForms.SampleApp;

[ApiController]
[Route("invitations")]
public sealed class InvitationController(FormsApplicationService formsService) : ControllerBase
{
    [Authorize(Policy = AuthPolicies.InvitationManage)]
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        var invitation = await formsService.CreateInvitationAsync(request, cancellationToken);
        return Ok(invitation);
    }

    [Authorize(Policy = AuthPolicies.Authenticated)]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromForm] string token, CancellationToken cancellationToken)
    {
        var invitation = await formsService.AcceptInvitationAsync(token, cancellationToken);
        return Ok(invitation);
    }

    [Authorize(Policy = AuthPolicies.InvitationManage)]
    [HttpPost("revoke/{invitationId:guid}")]
    public async Task<IActionResult> Revoke([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await formsService.RevokeInvitationAsync(invitationId, cancellationToken);
        return Ok(invitation);
    }
}
