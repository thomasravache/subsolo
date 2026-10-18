using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.RequestMagicLink;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0021 UT-01 (Emenda 3) — o limite de links mágicos por e-mail vem de <c>Users:MagicLinkEmailLimit</c>: sem a
/// chave, o padrão é 3 (o 4º pedido em 15 min não manda e-mail); com 5, o 6º é o primeiro barrado. O handler recebe as
/// opções do módulo como dependência obrigatória (sem valor padrão que ignore a configuração).
/// </summary>
public sealed class MagicLinkEmailLimitConfigurationTests
{
    private const string Email = "limite.configurado@subsolo.test";

    private static UsersOptions Bind(IReadOnlyDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new UsersOptions();
        configuration.GetSection(UsersOptions.SectionName).Bind(options);
        return options;
    }

    private static async Task<int> SentAfterAsync(MagicLinkHarness harness, int requests)
    {
        for (var i = 0; i < requests; i++)
        {
            (await harness.RequestAsync(Email)).IsSuccess.ShouldBeTrue();
        }

        return harness.Notifications.Requests.Count;
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task WithoutTheKeyTheLimitIs3()
    {
        var options = Bind(new Dictionary<string, string?>(StringComparer.Ordinal));
        options.MagicLinkEmailLimit.ShouldBe(3, "padrão da SPEC-0016");

        (await SentAfterAsync(new MagicLinkHarness(options), 4)).ShouldBe(3, "sem a chave, o 4º pedido em 15 min não manda e-mail");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task TheConfiguredLimitIsHonoured()
    {
        var options = Bind(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Users:MagicLinkEmailLimit"] = "5" });
        options.MagicLinkEmailLimit.ShouldBe(5);

        (await SentAfterAsync(new MagicLinkHarness(options), 6)).ShouldBe(5, "com Users:MagicLinkEmailLimit = 5, o 6º pedido é o barrado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public void TheHandlerRequiresTheUsersOptions()
    {
        var constructor = typeof(RequestMagicLinkCommandHandler)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldHaveSingleItem();
        var users = constructor.GetParameters()
            .Where(parameter => parameter.ParameterType == typeof(IOptions<UsersOptions>))
            .ShouldHaveSingleItem("o handler recebe IOptions<UsersOptions>");

        users.IsOptional.ShouldBeFalse("IOptions<UsersOptions> é obrigatório (sem valor padrão que ignore Users:MagicLinkEmailLimit)");
        new NullabilityInfoContext().Create(users).WriteState
            .ShouldBe(NullabilityState.NotNull, "IOptions<UsersOptions> não é anulável");
    }
}
