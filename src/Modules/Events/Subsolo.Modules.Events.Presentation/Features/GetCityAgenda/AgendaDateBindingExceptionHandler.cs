using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Subsolo.Modules.Events.Presentation.Features.GetCityAgenda;

/// <summary>
/// SPEC-0025 — com <c>ThrowOnBadRequest</c> (Development), a leitura de <c>from</c>/<c>to</c> mal formados na agenda
/// lança <see cref="BadHttpRequestException"/>: aqui ela vira o mesmo 400 <c>agenda.invalid_range</c> dos demais
/// ambientes (<see cref="AgendaDateBinding"/>). Outros endpoints e exceções seguem para os demais tratadores.
/// </summary>
internal sealed class AgendaDateBindingExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest }
            || endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName != GetCityAgendaEndpoint.Name)
        {
            return false;
        }

        await AgendaDateBinding.Problem(httpContext.Request).ExecuteAsync(httpContext);
        return true;
    }
}
