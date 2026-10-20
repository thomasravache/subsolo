using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Site.Application;
using Subsolo.Modules.Site.Application.Features.AddSupporter;
using Subsolo.Modules.Site.Application.Features.RemoveSupporter;

namespace Subsolo.Modules.Site.Presentation.Features;

/// <summary>
/// SPEC-0028 — <c>POST /api/v1/admin/supporters</c> (201 <c>{ id }</c>; inválido → 400 <c>site.invalid_supporter</c>)
/// e <c>DELETE /api/v1/admin/supporters/{id}</c> (204; inexistente → 404 <c>site.supporter_not_found</c>): política
/// <c>admin</c>, X-CSRF, rate limit <c>default</c>.
/// </summary>
internal sealed class AdminSupportersEndpoints : IEndpoint
{
    public const string Route = "/api/v1/admin/supporters";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(Route, AddAsync)
            .RequireSiteAdmin()
            .WithName("AddSupporter")
            .WithSummary("Adicionar apoiador")
            .WithDescription(
                "Cadastra o apoiador com nome de 2 a 60 caracteres e link https opcional (null para nenhum); ele entra no " +
                "fim da lista pública. Exige o cabeçalho X-CSRF.")
            .Produces<SupporterCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, SiteErrors.InvalidSupporter);

        app.MapDelete($"{Route}/{{id:guid}}", RemoveAsync)
            .RequireSiteAdmin()
            .WithName("RemoveSupporter")
            .WithSummary("Remover apoiador")
            .WithDescription("Remove o apoiador da lista pública. Exige o cabeçalho X-CSRF.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblemCode(StatusCodes.Status404NotFound, SiteErrors.SupporterNotFound);
    }

    private static async Task<IResult> AddAsync(
        SupporterRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<AddSupporterCommand> validator,
        [FromServices] ICommandHandler<AddSupporterCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var command = new AddSupporterCommand(request.Name, request.Url, SiteEndpoints.AdminId(user));
        if (await SiteEndpoints.ValidateAsync(validator, command, "Apoiador inválido.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"{Route}/{result.Value}", new SupporterCreatedResponse(result.Value))
            : ApiResults.Problem(result);
    }

    private static async Task<IResult> RemoveAsync(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<RemoveSupporterCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RemoveSupporterCommand(id, SiteEndpoints.AdminId(user)), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(result);
    }
}

/// <summary>SPEC-0028 — corpo do cadastro de apoiador: nome (2 a 60 caracteres) e link https (ou null).</summary>
internal sealed record SupporterRequest(string Name, string? Url);

/// <summary>SPEC-0028 — resposta do cadastro: só o id do apoiador novo.</summary>
internal sealed record SupporterCreatedResponse(Guid Id);
