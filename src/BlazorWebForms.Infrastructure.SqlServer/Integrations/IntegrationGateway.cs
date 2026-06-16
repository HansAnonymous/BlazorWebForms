using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class IntegrationGateway(
    IEmailIntegration email,
    IGraphIntegration graph,
    IPdfIntegration pdf) : IIntegrationGateway
{
    public IEmailIntegration Email { get; } = email;
    public IGraphIntegration Graph { get; } = graph;
    public IPdfIntegration Pdf { get; } = pdf;
}
