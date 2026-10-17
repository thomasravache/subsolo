using Microsoft.Extensions.Logging;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-05 (contrato v2) — guarda do atributo <c>event.name</c>: o pipeline de logs do ServiceDefaults grava
/// o <c>EventId.Name</c> como o atributo <c>event.name</c> do registro exportado, que é o que o filtro do alerta
/// "Falha de job" (<c>infra/seq/alerts.json</c>, ver <see cref="JobFailureAlertFilterTests"/>) consulta no Seq.
/// </summary>
public sealed class EventNameAttributeTests
{
    [Fact]
    [Trait("Spec", "SPEC-0009:UT-05")]
    public void EventNameIsExportedAsTheEventNameAttribute()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger("Subsolo.Observability.UnitTests");

#pragma warning disable CA1848 // log montado no teste
        logger.Log(LogLevel.Error, new EventId(1, "OutboxProcessingFailed"), "A execução do job falhou");
#pragma warning restore CA1848

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        (record.Attributes ?? []).Where(attribute => attribute.Key == "event.name")
            .ShouldHaveSingleItem("o registro exportado não traz exatamente um atributo event.name")
            .Value.ShouldBe("OutboxProcessingFailed", "o atributo event.name não traz o nome do evento");
    }
}
