using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Decorators;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-03 (contrato v2) — um log emitido dentro de um handler de módulo traz <c>module</c> e
/// <c>feature</c>, e o resource do log exportado traz <c>service.name=api</c> e <c>deployment.environment</c>.
/// A consulta real do módulo Events (<see cref="GetCityAgendaQuery"/>) passa pelo
/// <see cref="LoggingDecorator.QueryHandler{TQuery, TResponse}"/> (o mesmo que o Scrutor aplica nos módulos), com o
/// logger do pipeline do ServiceDefaults (ambiente <c>Development</c> e o <c>OTEL_SERVICE_NAME</c> que o AppHost
/// injeta). O handler interno é um dublê: a identidade do módulo e da feature vem da mensagem
/// (<c>Subsolo.Modules.Events.Application.Features.GetCityAgenda</c>), que é o que o decorador conhece.
/// </summary>
public sealed partial class ModuleLogFieldsTests
{
    private const string InnerMessage = "SPEC-0009:UT-03 log de dentro do handler";

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-03")]
    public async Task LogsFromAModuleHandlerCarryModuleFeatureAndResource()
    {
        using var telemetry = new ServiceDefaultsTelemetry(
            "Development",
            new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "api" });
        var services = telemetry.Services;
        var inner = new AgendaHandlerDouble(services.GetRequiredService<ILogger<AgendaHandlerDouble>>());
        var decorated = new LoggingDecorator.QueryHandler<GetCityAgendaQuery, CityAgendaResponse>(
            inner,
            services.GetRequiredService<ILogger<LoggingDecorator.QueryHandler<GetCityAgendaQuery, CityAgendaResponse>>>());

        var result = await decorated.Handle(new GetCityAgendaQuery("cidade-exemplo"), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();

        var logs = telemetry.Logs;
        var insideHandler = logs.Where(log => log.FormattedMessage == InnerMessage)
            .ShouldHaveSingleItem("o log do handler não foi exportado pelo pipeline do ServiceDefaults");
        var fromDecorator = logs.Where(log => log.CategoryName?.Contains(nameof(LoggingDecorator), StringComparison.Ordinal) == true)
            .ToList();
        fromDecorator.ShouldNotBeEmpty("os logs do LoggingDecorator não foram exportados");

        var resource = telemetry.LogResource;
        logs.ShouldSatisfyAllConditions(
            () => ExportedLog.Fields(insideHandler).ShouldContainKeyAndValue("module", "events",
                "o log emitido dentro do handler não traz module=events"),
            () => ExportedLog.Fields(insideHandler).ShouldContainKeyAndValue("feature", "GetCityAgenda",
                "o log emitido dentro do handler não traz feature=GetCityAgenda"),
            () => fromDecorator.ShouldAllBe(
                log => ExportedLog.Fields(log).GetValueOrDefault("module") == "events" &&
                       ExportedLog.Fields(log).GetValueOrDefault("feature") == "GetCityAgenda",
                "os logs do LoggingDecorator não trazem module=events e feature=GetCityAgenda"),
            () => resource.ShouldContainKeyAndValue("service.name", "api",
                "o resource dos logs não traz service.name=api"),
            () => resource.ShouldContainKeyAndValue("deployment.environment", "Development",
                "o resource dos logs não traz deployment.environment=Development"));
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-03")]
    public void SpanResourceCarriesServiceNameAndEnvironment()
    {
        using var telemetry = new ServiceDefaultsTelemetry(
            "Development",
            new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "api" });

        var resource = telemetry.SpanResource;
        resource.ShouldSatisfyAllConditions(
            () => resource.ShouldContainKeyAndValue("service.name", "api", "o resource dos spans não traz service.name=api"),
            () => resource.ShouldContainKeyAndValue("deployment.environment", "Development",
                "o resource dos spans não traz deployment.environment=Development"));
    }

    /// <summary>Dublê do handler da agenda: loga de dentro do handler e devolve uma agenda vazia.</summary>
    private sealed partial class AgendaHandlerDouble(ILogger<AgendaHandlerDouble> logger)
        : IQueryHandler<GetCityAgendaQuery, CityAgendaResponse>
    {
        public Task<Result<CityAgendaResponse>> Handle(GetCityAgendaQuery query, CancellationToken cancellationToken)
        {
            LogInside(logger);
            return Task.FromResult(Result.Success(new CityAgendaResponse(
                new CityResponse(query.Slug, "Cidade Exemplo", "SP", "America/Sao_Paulo"), [])));
        }

        [LoggerMessage(Level = LogLevel.Information, Message = InnerMessage)]
        private static partial void LogInside(ILogger logger);
    }
}
