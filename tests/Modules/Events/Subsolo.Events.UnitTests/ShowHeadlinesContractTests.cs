using System.Reflection;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0033 CT-01 (Emenda 2, contrato v3) — o contrato público do Events declara a leitura em lote para a fila de
/// denúncias: <c>public interface IShowHeadlines { Task&lt;IReadOnlyDictionary&lt;Guid, ShowHeadline&gt;&gt;
/// GetAsync(IReadOnlyCollection&lt;Guid&gt; showIds, CancellationToken ct); }</c>, interface própria (fora do
/// <see cref="IEventsCatalog"/>), e <c>public sealed record ShowHeadline(Guid ShowId, string Title, string CitySlug,
/// string Slug, DateTimeOffset StartsAtUtc, string VenueName)</c>, ambos em <c>Subsolo.Modules.Events.IntegrationEvents</c>.
/// </summary>
public sealed class ShowHeadlinesContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0033:CT-01")]
    public void IShowHeadlinesHasOnlyTheBatchGetOfTheContract()
    {
        var type = typeof(IShowHeadlines);

        type.IsInterface.ShouldBeTrue();
        type.IsPublic.ShouldBeTrue();
        type.Assembly.ShouldBe(typeof(IEventsCatalog).Assembly);
        type.Namespace.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");
        typeof(IEventsCatalog).IsAssignableFrom(type).ShouldBeFalse("IShowHeadlines é interface própria, não parte do IEventsCatalog");
        type.GetInterfaces().ShouldBeEmpty();
        var method = type.GetMethods().ShouldHaveSingleItem("IShowHeadlines tem métodos além do GetAsync");
        method.Name.ShouldBe("GetAsync");
        method.ReturnType.ShouldBe(typeof(Task<IReadOnlyDictionary<Guid, ShowHeadline>>));
        method.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType))
            .ShouldBe([("showIds", typeof(IReadOnlyCollection<Guid>)), ("ct", typeof(CancellationToken))]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:CT-01")]
    public void ShowHeadlineHasTheFieldsOfTheContract()
    {
        var type = typeof(ShowHeadline);

        type.IsPublic.ShouldBeTrue();
        type.IsSealed.ShouldBeTrue();
        type.Namespace.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First()
            .GetParameters()
            .Select(parameter => $"{parameter.Name}:{parameter.ParameterType.Name}")
            .ShouldBe([
                "ShowId:Guid",
                "Title:String",
                "CitySlug:String",
                "Slug:String",
                "StartsAtUtc:DateTimeOffset",
                "VenueName:String",
            ]);
    }
}
