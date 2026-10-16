using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0008 UT-04 — o endpoint <c>GET /api/v1/cities/{slug}/agenda</c>, mapeado pelos <c>IEndpoint</c> do módulo,
/// é público de forma explícita: a metadata contém <see cref="IAllowAnonymous"/>.
/// </summary>
public sealed class GetCityAgendaEndpointTests
{
    private const string Route = "/api/v1/cities/{slug}/agenda";

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task AgendaEndpointMetadataContainsAllowAnonymous()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        builder.Services.AddEndpoints(Assembly.Load("Subsolo.Modules.Events.Presentation"));
        // Só a presença no contêiner importa (inferência dos parâmetros do Minimal API); nada é resolvido aqui.
        builder.Services.AddScoped<IValidator<GetCityAgendaQuery>>(_ => throw new InvalidOperationException());
        builder.Services.AddScoped<IQueryHandler<GetCityAgendaQuery, CityAgendaResponse>>(
            _ => throw new InvalidOperationException());
        await using var app = builder.Build();

        app.MapEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList();
        var agenda = endpoints.OfType<RouteEndpoint>().SingleOrDefault(endpoint => endpoint.RoutePattern.RawText == Route);
        agenda.ShouldNotBeNull($"endpoint {Route} não mapeado; mapeados: {string.Join(", ", endpoints.Select(e => e.DisplayName))}");
        agenda.Metadata.GetMetadata<IAllowAnonymous>().ShouldNotBeNull();
    }
}
