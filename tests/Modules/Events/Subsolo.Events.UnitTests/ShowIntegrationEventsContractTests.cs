using System.Reflection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0023 CT-01 (eventos) — os tipos públicos de <c>Subsolo.Modules.Events.IntegrationEvents</c> têm os campos do §6:
/// <c>ShowPublishedIntegrationEvent(ShowId, City, ProfileIds, StartsAtUtc)</c> e
/// <c>ShowChangedIntegrationEvent(ShowId, Status, StartsAtUtc)</c>, ambos eventos de integração (<see cref="IIntegrationEvent"/>)
/// selados. O snapshot OpenAPI e o <c>pnpm api:check</c> ficam no <c>web/src/api/shows-contract.test.ts</c>.
/// </summary>
public sealed class ShowIntegrationEventsContractTests
{
    [Theory]
    [Trait("Spec", "SPEC-0023:CT-01")]
    [InlineData(typeof(ShowPublishedIntegrationEvent), new[] { "ShowId:Guid", "City:String", "ProfileIds:IReadOnlyList`1[Guid]", "StartsAtUtc:DateTimeOffset" })]
    [InlineData(typeof(ShowChangedIntegrationEvent), new[] { "ShowId:Guid", "Status:String", "StartsAtUtc:DateTimeOffset" })]
    public void TheShowIntegrationEventsHaveTheFieldsOfTheContract(Type type, string[] fields)
    {
        type.IsPublic.ShouldBeTrue($"{type.Name} não é público");
        type.IsSealed.ShouldBeTrue($"{type.Name} não é selado");
        typeof(IIntegrationEvent).IsAssignableFrom(type).ShouldBeTrue($"{type.Name} não é IIntegrationEvent");

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();
        constructor.GetParameters().Select(parameter => $"{parameter.Name}:{Describe(parameter.ParameterType)}")
            .ShouldBe(fields, $"campos de {type.Name} diferentes do §6");
    }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name}[{string.Join(",", type.GetGenericArguments().Select(argument => argument.Name))}]"
            : type.Name;
}
