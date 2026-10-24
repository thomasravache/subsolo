using System.Reflection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0033 CT-01 (evento) — o contrato público do Events (<c>Subsolo.Modules.Events.IntegrationEvents</c>) declara
/// <c>public sealed record ShowHiddenByModerationIntegrationEvent(Guid ShowId, DateTimeOffset HiddenAtUtc)</c>, evento de
/// integração (<see cref="IIntegrationEvent"/>) publicado pelo Moderation e consumido pelo Events (padrão do consumidor,
/// ADR-0001). O tipo é procurado pelo nome, para o teste compilar antes de ele existir. O OpenAPI fica no
/// <c>ModerationOpenApiContractTests</c> (Api.IntegrationTests) e no <c>web/src/api/reports-contract.test.ts</c>.
/// </summary>
public sealed class ShowHiddenByModerationContractTests
{
    private const string TypeName = "Subsolo.Modules.Events.IntegrationEvents.ShowHiddenByModerationIntegrationEvent";

    [Fact]
    [Trait("Spec", "SPEC-0033:CT-01")]
    public void TheShowHiddenEventHasTheFieldsOfTheContract()
    {
        var type = typeof(IEventsCatalog).Assembly.GetType(TypeName);

        type.ShouldNotBeNull($"{TypeName} não existe no contrato do Events");
        type.IsPublic.ShouldBeTrue($"{type.Name} não é público");
        type.IsSealed.ShouldBeTrue($"{type.Name} não é selado");
        typeof(IIntegrationEvent).IsAssignableFrom(type).ShouldBeTrue($"{type.Name} não é IIntegrationEvent");
        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();
        constructor.GetParameters().Select(parameter => $"{parameter.Name}:{parameter.ParameterType.Name}")
            .ShouldBe(["ShowId:Guid", "HiddenAtUtc:DateTimeOffset"], $"campos de {type.Name} diferentes do §6");
    }
}
