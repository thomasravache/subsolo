using System.Reflection;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0031 CT-01 (contrato) — o tipo público <see cref="IEventsCatalog"/> tem
/// <c>Task&lt;ShowSummary?&gt; GetShowSummaryAsync(Guid showId, CancellationToken ct)</c> e o registro selado
/// <see cref="ShowSummary"/> tem os campos do §6:
/// <c>(ShowId, Slug, CitySlug, CityName, CityTimeZone, Lineup, VenueName, StartsAtUtc, Status)</c>. A parte do OpenAPI do
/// CT-01 fica no <c>InheritedOpenApiContractTests</c> (Notifications.IntegrationTests).
/// </summary>
public sealed class ShowSummaryContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0031:CT-01")]
    public void TheCatalogHasGetShowSummaryAsync()
    {
        var method = typeof(IEventsCatalog).GetMethod("GetShowSummaryAsync").ShouldNotBeNull("IEventsCatalog sem GetShowSummaryAsync");

        method.ReturnType.ShouldBe(typeof(Task<ShowSummary>));
        new NullabilityInfoContext().Create(method.ReturnParameter).GenericTypeArguments[0].ReadState
            .ShouldBe(NullabilityState.Nullable, "o resumo de um show que não existe é nulo (ShowSummary?)");
        method.GetParameters().Select(parameter => $"{parameter.Name}:{parameter.ParameterType.Name}")
            .ShouldBe(["showId:Guid", "ct:CancellationToken"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:CT-01")]
    public void TheShowSummaryHasTheFieldsOfTheContract()
    {
        var type = typeof(ShowSummary);
        type.IsPublic.ShouldBeTrue();
        type.IsSealed.ShouldBeTrue();
        type.Namespace.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        constructor.GetParameters().Select(parameter => $"{parameter.Name}:{Describe(parameter.ParameterType)}").ShouldBe(
        [
            "ShowId:Guid",
            "Slug:String",
            "CitySlug:String",
            "CityName:String",
            "CityTimeZone:String",
            "Lineup:IReadOnlyList`1[String]",
            "VenueName:String",
            "StartsAtUtc:DateTimeOffset",
            "Status:String",
        ]);
    }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name}[{string.Join(",", type.GetGenericArguments().Select(argument => argument.Name))}]"
            : type.Name;
}
