using System.Reflection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Profiles.IntegrationEvents;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Profiles.UnitTests;

/// <summary>
/// SPEC-0021 CT-01 (parte .NET) — o contrato público consumido pelo cadastro de show tem a assinatura do §6:
/// <see cref="IProfilesApi"/> com <c>GetOwnedProfilesAsync(Guid, CancellationToken)</c> →
/// <c>Task&lt;IReadOnlyList&lt;ProfileSummary&gt;&gt;</c> e <c>GetAsync(Guid, CancellationToken)</c> →
/// <c>Task&lt;ProfileSummary?&gt;</c>; <see cref="ProfileSummary"/>(Id, Type, Name, Slug, City, OwnerUserId) sem contato
/// nem prova. Emenda v2: <see cref="ArtistRoleGrantedIntegrationEvent"/>(UserId, ProfileId), só com ids, e
/// <see cref="IUsersDirectory"/>.<c>GetEmailsAsync(IReadOnlyCollection&lt;Guid&gt;, CancellationToken)</c> →
/// <c>Task&lt;IReadOnlyDictionary&lt;Guid, string&gt;&gt;</c> ficam no contrato do Users (consumidor), não no Profiles.
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
    public void ArtistRoleGrantedLivesInTheUsersContractAndCarriesOnlyIds()
    {
        var type = typeof(ArtistRoleGrantedIntegrationEvent);
        type.Namespace.ShouldBe("Subsolo.Modules.Users.IntegrationEvents");
        type.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Users.IntegrationEvents");
        typeof(IIntegrationEvent).IsAssignableFrom(type).ShouldBeTrue();
        type.GetConstructors()
            .Select(constructor => string.Join(", ", constructor.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}")))
            .ShouldContain("Guid UserId, Guid ProfileId");
        type.GetProperties().Where(property => property.PropertyType == typeof(string)).ShouldBeEmpty("o evento leva só ids");
        typeof(IProfilesApi).Assembly.GetExportedTypes()
            .ShouldNotContain(exported => typeof(IIntegrationEvent).IsAssignableFrom(exported),
                "o Profiles.IntegrationEvents não define o evento do papel artist (Emenda v2)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:CT-01")]
    public void IUsersDirectoryHasTheSignatureOfTheContract()
    {
        var directory = typeof(IUsersDirectory);
        directory.IsInterface.ShouldBeTrue();
        directory.Namespace.ShouldBe("Subsolo.Modules.Users.IntegrationEvents");
        directory.GetMethods().Select(method => method.Name).ShouldBe(["GetEmailsAsync"]);
        var emails = directory.GetMethod("GetEmailsAsync").ShouldNotBeNull();
        emails.GetParameters().Select(parameter => parameter.ParameterType)
            .ShouldBe([typeof(IReadOnlyCollection<Guid>), typeof(CancellationToken)]);
        emails.ReturnType.ShouldBe(typeof(Task<IReadOnlyDictionary<Guid, string>>));
    }
}
