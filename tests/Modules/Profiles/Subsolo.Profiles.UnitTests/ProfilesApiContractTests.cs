using System.Reflection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Profiles.UnitTests;

/// <summary>
/// SPEC-0021 CT-01 (parte .NET) — o contrato público consumido pelo cadastro de show tem a assinatura do §6:
/// <see cref="IProfilesApi"/> com <c>GetOwnedProfilesAsync(Guid, CancellationToken)</c> →
/// <c>Task&lt;IReadOnlyList&lt;ProfileSummary&gt;&gt;</c> e <c>GetAsync(Guid, CancellationToken)</c> →
/// <c>Task&lt;ProfileSummary?&gt;</c>; <see cref="ProfileSummary"/>(Id, Type, Name, Slug, City, OwnerUserId) sem contato
/// nem prova; e <see cref="ProfileOwnerAssignedIntegrationEvent"/>(ProfileId, UserId), só com ids.
/// </summary>
public sealed class ProfilesApiContractTests
{
    [Fact]
    [Trait("Spec", "SPEC-0021:CT-01")]
    public void IProfilesApiHasTheSignatureOfTheContract()
    {
        var api = typeof(IProfilesApi);
        api.IsInterface.ShouldBeTrue();
        api.IsPublic.ShouldBeTrue();
        api.GetMethods().Select(method => method.Name).ShouldBe(["GetOwnedProfilesAsync", "GetAsync"], ignoreOrder: true);

        var owned = api.GetMethod("GetOwnedProfilesAsync").ShouldNotBeNull();
        owned.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe([typeof(Guid), typeof(CancellationToken)]);
        owned.ReturnType.ShouldBe(typeof(Task<IReadOnlyList<ProfileSummary>>));

        var get = api.GetMethod("GetAsync").ShouldNotBeNull();
        get.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe([typeof(Guid), typeof(CancellationToken)]);
        get.ReturnType.ShouldBe(typeof(Task<ProfileSummary>));
        new NullabilityInfoContext().Create(get.ReturnParameter).GenericTypeArguments.ShouldHaveSingleItem()
            .ReadState.ShouldBe(NullabilityState.Nullable, "GetAsync devolve null para perfil inexistente");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:CT-01")]
    public void ProfileSummaryHasTheFieldsOfTheContractAndNoContact()
    {
        var constructor = typeof(ProfileSummary).GetConstructors().ShouldHaveSingleItem();

        constructor.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType)).ShouldBe(
        [
            ("Id", typeof(Guid)),
            ("Type", typeof(string)),
            ("Name", typeof(string)),
            ("Slug", typeof(string)),
            ("City", typeof(string)),
            ("OwnerUserId", typeof(Guid?)),
        ]);
        typeof(ProfileSummary).GetProperties().Select(property => property.Name)
            .ShouldNotContain(name => name.Contains("Contact", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Proof", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:CT-01")]
    public void ProfileOwnerAssignedCarriesOnlyIds()
    {
        typeof(IIntegrationEvent).IsAssignableFrom(typeof(ProfileOwnerAssignedIntegrationEvent)).ShouldBeTrue();
        var signatures = typeof(ProfileOwnerAssignedIntegrationEvent).GetConstructors()
            .Select(constructor => string.Join(", ", constructor.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}")))
            .ToList();
        signatures.ShouldContain("Guid ProfileId, Guid UserId");
        typeof(ProfileOwnerAssignedIntegrationEvent).GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .ShouldBeEmpty("o evento leva só ids");
    }
}
