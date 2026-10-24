using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0032 — o contrato do <see cref="IEventsCatalog"/> (§6, contrato v2), lido por reflexão:
/// <list type="bullet">
/// <item>CT-01 — <c>Task&lt;IReadOnlyList&lt;ShowSummary&gt;&gt; ListScheduledShowsStartingAsync(DateTimeOffset fromUtc,
/// DateTimeOffset toUtc, CancellationToken ct)</c> (a parte do OpenAPI do CT-01, que não muda, fica no
/// <c>ReminderOpenApiContractTests</c> do Api.IntegrationTests);</item>
/// <item>H29-01 — <c>Task&lt;IReadOnlyDictionary&lt;Guid, ShowState&gt;&gt; GetShowStatesAsync(IReadOnlyCollection&lt;Guid&gt;
/// showIds, CancellationToken ct)</c>.</item>
/// </list>
/// </summary>
public sealed class EventsCatalogRemindersContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0032:CT-01")]
    public void TheCatalogHasListScheduledShowsStartingAsync()
    {
        var method = typeof(IEventsCatalog).GetMethod("ListScheduledShowsStartingAsync")
            .ShouldNotBeNull("IEventsCatalog sem ListScheduledShowsStartingAsync");

        method.ReturnType.ShouldBe(typeof(Task<IReadOnlyList<ShowSummary>>));
        method.GetParameters().Select(parameter => $"{parameter.Name}:{parameter.ParameterType.Name}")
            .ShouldBe(["fromUtc:DateTimeOffset", "toUtc:DateTimeOffset", "ct:CancellationToken"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H29-01")]
    public void TheCatalogHasGetShowStatesAsync()
    {
        var method = typeof(IEventsCatalog).GetMethod("GetShowStatesAsync")
            .ShouldNotBeNull("IEventsCatalog sem GetShowStatesAsync");

        method.ReturnType.ShouldBe(typeof(Task<IReadOnlyDictionary<Guid, ShowState>>));
        method.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType))
            .ShouldBe([("showIds", typeof(IReadOnlyCollection<Guid>)), ("ct", typeof(CancellationToken))]);
    }
}
