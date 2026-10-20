using System.Reflection;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0029 CT-01 (parte .NET) — as interfaces públicas do §6: <see cref="IAttendanceApi"/> (público, no assembly
/// <c>Subsolo.Modules.Engagement.IntegrationEvents</c>) com <c>CountAsync(IReadOnlyCollection&lt;Guid&gt;,
/// CancellationToken)</c> → <c>Task&lt;IReadOnlyDictionary&lt;Guid, int&gt;&gt;</c> e <c>GetAttendeesAsync(Guid,
/// CancellationToken)</c> → <c>Task&lt;IReadOnlyCollection&lt;Guid&gt;&gt;</c> (só ids e números); e o
/// <see cref="IEventsCatalog"/> com <c>GetShowStateAsync(Guid, CancellationToken)</c> → <c>Task&lt;ShowState?&gt;</c>,
/// <see cref="ShowState"/>(Status, StartsAtUtc).
/// </summary>
public sealed class AttendanceApiContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0029:CT-01")]
    public void IAttendanceApiHasTheSignatureOfTheContract()
    {
        var api = typeof(IAttendanceApi);
        api.IsInterface.ShouldBeTrue();
        api.IsPublic.ShouldBeTrue();
        api.Namespace.ShouldBe("Subsolo.Modules.Engagement.IntegrationEvents");
        api.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Engagement.IntegrationEvents");
        api.GetMethods().Select(method => method.Name).ShouldBe(["CountAsync", "GetAttendeesAsync"], ignoreOrder: true);

        var count = api.GetMethod("CountAsync").ShouldNotBeNull();
        count.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType)).ShouldBe(
        [
            ("showIds", typeof(IReadOnlyCollection<Guid>)),
            ("ct", typeof(CancellationToken)),
        ]);
        count.ReturnType.ShouldBe(typeof(Task<IReadOnlyDictionary<Guid, int>>));

        var attendees = api.GetMethod("GetAttendeesAsync").ShouldNotBeNull();
        attendees.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType)).ShouldBe(
        [
            ("showId", typeof(Guid)),
            ("ct", typeof(CancellationToken)),
        ]);
        attendees.ReturnType.ShouldBe(typeof(Task<IReadOnlyCollection<Guid>>));
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:CT-01")]
    public void TheEventsCatalogReadsTheShowState()
    {
        var state = typeof(IEventsCatalog).GetMethod("GetShowStateAsync").ShouldNotBeNull();
        state.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType)).ShouldBe(
        [
            ("showId", typeof(Guid)),
            ("ct", typeof(CancellationToken)),
        ]);
        state.ReturnType.ShouldBe(typeof(Task<ShowState>));
        new NullabilityInfoContext().Create(state.ReturnParameter).GenericTypeArguments.ShouldHaveSingleItem()
            .ReadState.ShouldBe(NullabilityState.Nullable, "GetShowStateAsync devolve null para show inexistente");

        typeof(ShowState).Namespace.ShouldBe("Subsolo.Modules.Events.IntegrationEvents");
        typeof(ShowState).GetConstructors().ShouldHaveSingleItem().GetParameters()
            .Select(parameter => (parameter.Name, parameter.ParameterType))
            .ShouldBe([("Status", typeof(string)), ("StartsAtUtc", typeof(DateTimeOffset))]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:CT-01")]
    public void TheContractAssemblyExposesNoDomainTypes()
    {
        var exported = typeof(IAttendanceApi).Assembly.GetExportedTypes();

        exported.ShouldContain(typeof(IAttendanceApi));
        exported.ShouldNotContain(type => type.Namespace != null && type.Namespace.Contains(".Domain", StringComparison.Ordinal));
    }
}
