using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0027 CT-01 (parte .NET) — o contrato público lido pelos avisos tem a assinatura do §6:
/// <see cref="IEngagementApi"/>, público, no namespace e no assembly <c>Subsolo.Modules.Engagement.IntegrationEvents</c>,
/// com um só método, <c>GetFollowersAsync(string, IReadOnlyCollection&lt;Guid&gt;, CancellationToken)</c> →
/// <c>Task&lt;IReadOnlyCollection&lt;Guid&gt;&gt;</c>; o assembly não expõe tipos do domínio (só ids).
/// </summary>
public sealed class EngagementApiContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0027:CT-01")]
    public void IEngagementApiHasTheSignatureOfTheContract()
    {
        var api = typeof(IEngagementApi);
        api.IsInterface.ShouldBeTrue();
        api.IsPublic.ShouldBeTrue();
        api.Namespace.ShouldBe("Subsolo.Modules.Engagement.IntegrationEvents");
        api.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Engagement.IntegrationEvents");
        api.GetMethods().Select(method => method.Name).ShouldBe(["GetFollowersAsync"]);

        var followers = api.GetMethod("GetFollowersAsync").ShouldNotBeNull();
        followers.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType)).ShouldBe(
        [
            ("citySlug", typeof(string)),
            ("profileIds", typeof(IReadOnlyCollection<Guid>)),
            ("ct", typeof(CancellationToken)),
        ]);
        followers.ReturnType.ShouldBe(typeof(Task<IReadOnlyCollection<Guid>>));
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:CT-01")]
    public void TheContractAssemblyExposesOnlyTheInterface()
    {
        var exported = typeof(IEngagementApi).Assembly.GetExportedTypes();

        exported.ShouldNotContain(type => type.Namespace != null && type.Namespace.Contains(".Domain", StringComparison.Ordinal));
        exported.ShouldContain(typeof(IEngagementApi));
    }
}
