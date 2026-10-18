using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.UploadFlyer;

namespace Subsolo.Modules.Events.Presentation.Features.UploadFlyer;

/// <summary>
/// SPEC-0022 — <c>POST /api/v1/flyers</c> (artista ou admin, X-CSRF, rate limit <c>report</c>): o flyer em
/// <c>multipart/form-data</c>, campo <c>file</c>. O corpo é lido aqui, depois da autorização e do anti-forgery, com o
/// limite de 10 MB por parte: um envio maior para de ser lido no limite (e um <c>Content-Length</c> acima dele nem
/// começa a ser lido; SPEC-0023: o corpo inteiro tem o teto de 10 MB + a folga do multipart, com ou sem
/// <c>Content-Length</c>). 201 com o flyer novo; 200 com o mesmo flyer se o arquivo já foi enviado.
/// </summary>
internal sealed class UploadFlyerEndpoint : IEndpoint
{
    public const string Route = "/api/v1/flyers";

    public const string Tag = "Flyers";

    /// <summary>Nome do campo do arquivo no formulário.</summary>
    public const string FileField = "file";

    /// <summary>Folga para os cabeçalhos e delimitadores do multipart sobre os 10 MB do arquivo.</summary>
    private const long MultipartOverhead = 64 * 1024;

    private static readonly FormOptions UploadFormOptions = new()
    {
        MultipartBodyLengthLimit = FlyerUploadRules.MaxBytes,
        ValueCountLimit = 16,
    };

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, HandleAsync)
            .RequireAuthorization(policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AuthorizationPolicies.Artist, AuthorizationPolicies.Admin))
            .RequireRateLimiting(RateLimitPolicies.Report)
            .RequireNoStore()
            .WithTags(Tag)
            .WithName("UploadFlyer")
            .WithSummary("Enviar flyer")
            .WithDescription(
                "Envia o flyer de um show (artista ou admin) em multipart/form-data, campo file: JPEG, PNG ou WebP pelo " +
                "conteúdo, até 10 MB e 40 megapixels. Os metadados (EXIF, GPS) são removidos e saem as variantes thumb " +
                "(400 px, WebP), full (1080 px, WebP) e og (1200×630, JPEG), servidas em /media/flyers/ com cache " +
                "imutável. O id é o SHA-256 do arquivo: o mesmo arquivo de novo devolve 200 com o mesmo id, sem " +
                "reprocessar. Exige o cabeçalho X-CSRF.")
            .Accepts<UploadFlyerForm>("multipart/form-data")
            .Produces<FlyerUploadedResponse>(StatusCodes.Status201Created)
            .Produces<FlyerUploadedResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.FlyerInvalidType)
            .WithMetadata(
                new ProblemCodeMetadata(StatusCodes.Status400BadRequest, EventsErrors.FlyerTooLarge),
                new ProblemCodeMetadata(StatusCodes.Status400BadRequest, EventsErrors.FlyerTooManyPixels),
                new ProblemCodeMetadata(StatusCodes.Status400BadRequest, EventsErrors.FlyerMissing),
                new ProblemCodeMetadata(StatusCodes.Status400BadRequest, SecurityProblems.CsrfInvalid))
            .ProducesProblemCode(StatusCodes.Status422UnprocessableEntity, EventsErrors.FlyerUnprocessable)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status403Forbidden, SecurityProblems.AuthForbidden)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UploadFlyerCommand, FlyerUploadResult> handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(SessionAuthentication.UserIdClaim), CultureInfo.InvariantCulture, out var userId))
        {
            return SecurityProblems.Unauthorized();
        }

        if (request.ContentLength > FlyerUploadRules.MaxBytes + MultipartOverhead)
        {
            return Problem(EventsErrors.FlyerTooLargeError());
        }

        if (!request.HasFormContentType)
        {
            return Problem(EventsErrors.FlyerMissingError());
        }

        // SPEC-0023 (H22-03): teto do corpo inteiro antes de ler, também sem Content-Length (corpo chunked).
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
        {
            bodyLimit.MaxRequestBodySize = FlyerUploadRules.MaxBytes + MultipartOverhead;
        }

        IFormCollection form;
        try
        {
            request.HttpContext.Features.Set<IFormFeature>(new FormFeature(request, UploadFormOptions));
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Problem(EventsErrors.FlyerTooLargeError());
        }
        catch (InvalidDataException)
        {
            // Parte acima do limite de 10 MB (a leitura para ali) ou multipart malformado.
            return Problem(request.ContentLength is null or > FlyerUploadRules.MaxBytes
                ? EventsErrors.FlyerTooLargeError()
                : EventsErrors.FlyerMissingError());
        }

        if (form.Files.GetFile(FileField) is not { Length: > 0 } file)
        {
            return Problem(EventsErrors.FlyerMissingError());
        }

        await using var content = file.OpenReadStream();
        var result = await handler.Handle(new UploadFlyerCommand(content, file.Length, userId), cancellationToken);
        if (result.IsFailure)
        {
            // SPEC-0023 (H22-05): a fila de processamento não liberou a vez a tempo.
            return result.Error.Code == MediaErrors.Busy ? SecurityProblems.Unavailable() : ApiResults.Problem(result);
        }

        return result.Value.Created
            ? Results.Created((string?)null, result.Value.Flyer)
            : Results.Ok(result.Value.Flyer);
    }

    private static IResult Problem(Error error) => ApiResults.Problem(Result.Failure(error));
}

/// <summary>SPEC-0022 — o formulário do envio (só para o OpenAPI): o arquivo no campo <c>file</c>.</summary>
internal sealed record UploadFlyerForm(IFormFile File);
