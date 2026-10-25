using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Application.Features.ReportShow;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Presentation.Features;

/// <summary>
/// SPEC-0033 — <c>POST /api/v1/shows/{id}/reports</c> (política <c>fan</c>, X-CSRF, limite <c>moderation-report</c>): o
/// fã denuncia o show com o motivo e o detalhe opcional. 201 <c>{ id }</c>.
/// </summary>
internal sealed class ReportShowEndpoint : IEndpoint
{
    public const string Route = "/api/v1/shows/{id:guid}/reports";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireModerationFan()
            .WithName("ReportShow")
            .WithSummary("Denunciar show")
            .WithDescription(
                "Denuncia o show com o motivo (fake, cancelled_unannounced, wrong_info ou other) e o detalhe opcional " +
                "(até 500 caracteres; obrigatório em other). Uma denúncia pendente por fã e show; até 10 denúncias por " +
                "fã por dia. Exige o cabeçalho X-CSRF.")
            .Produces<ReportCreated>(StatusCodes.Status201Created)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, ModerationErrors.InvalidReport)
            .ProducesProblemCode(StatusCodes.Status404NotFound, ModerationErrors.ShowNotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, ModerationErrors.AlreadyReported);

    private static async Task<IResult> HandleAsync(
        Guid id,
        ReportShowRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<ReportShowCommand> validator,
        [FromServices] ICommandHandler<ReportShowCommand, ReportCreated> handler,
        CancellationToken cancellationToken)
    {
        if (ModerationEndpoints.UserId(user) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var command = new ReportShowCommand(userId, id, request.Reason, request.Details);
        if (await EndpointValidation.ValidateAsync(validator, command, "Denúncia inválida.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(command, cancellationToken);
        return result.IsSuccess ? Results.Created((string?)null, result.Value) : ApiResults.Problem(result);
    }
}

/// <summary>
/// SPEC-0033 — corpo da denúncia: o motivo e o detalhe opcional. SPEC-0037 (H33-07): <see cref="AllowedValuesAttribute"/>
/// só documenta no OpenAPI o enum dos quatro motivos; quem recusa o motivo fora da lista (400
/// <c>moderation.invalid_report</c>) é o validador. SPEC-0039 (H37-01): <c>Reason</c> não é anulável em C# (o validador já
/// recusa o nulo, e o OpenAPI o documenta sem <c>null</c>); ausente ou nulo no JSON ainda chega nulo e dá o mesmo 400.
/// </summary>
internal sealed record ReportShowRequest(
    [property: AllowedValues(ReportReasons.Fake, ReportReasons.CancelledUnannounced, ReportReasons.WrongInfo, ReportReasons.Other)]
    string Reason,
    string? Details);
