using System.Text.Json;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0019 UT-02 — com um usuário de 2 sessões (e outro usuário com a sua) e duas <c>IPersonalDataSection</c> de teste
/// (uma devolve objeto, outra <see langword="null"/>), o export traz a conta, as 2 sessões do usuário sem nenhum campo de
/// hash ou token, a seção que devolveu objeto pelo nome dela, e não traz a que devolveu <see langword="null"/> nem nada
/// do outro usuário.
/// </summary>
public sealed class ExportPersonalDataTests
{
    private const string Email = "fulano@email.com";
    private const string OtherEmail = "outra.pessoa@email.com";

    [Fact]
    [Trait("Spec", "SPEC-0019:UT-02")]
    public async Task ExportHasTheAccountTheSessionsAndTheNonNullSections()
    {
        var follows = new { cities = "curitiba" };
        var followsSection = new FixedPersonalDataSection("follows", follows);
        var emptySection = new FixedPersonalDataSection("going", null);
        var harness = new AccountHarness(followsSection, emptySection);
        var user = await harness.SignInAsync(Email);
        harness.Base.Clock.Advance(TimeSpan.FromHours(2));
        await harness.SignInAsync(Email);
        await harness.SignInAsync(OtherEmail);
        (await harness.RenameAsync(user.Id, "Caio Grind")).IsSuccess.ShouldBeTrue();
        harness.Base.Clock.Advance(TimeSpan.FromMinutes(5));
        var sessions = harness.Base.Sessions.All.Where(session => session.UserId == user.Id).ToList();
        sessions.Count.ShouldBe(2);

        var result = await harness.ExportAsync(user.Id);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var export = result.Value;
        export.ExportedAtUtc.ShouldBe(harness.Base.Clock.GetUtcNow());

        export.Account.Id.ShouldBe(user.Id);
        export.Account.Email.ShouldBe(Email);
        export.Account.DisplayName.ShouldBe("Caio Grind");
        export.Account.Roles.ShouldBe(user.Roles);
        export.Account.CreatedAtUtc.ShouldBe(user.CreatedAtUtc);

        export.Sessions.Count.ShouldBe(2, "só as 2 sessões do usuário (nenhuma do outro)");
        foreach (var session in sessions)
        {
            export.Sessions.ShouldContain(
                exported => exported.CreatedAtUtc == session.CreatedAtUtc
                    && exported.Method == SignInMethods.MagicLink
                    && exported.LastSeenAtUtc == session.LastSeenAtUtc
                    && exported.ExpiresAtUtc == session.ExpiresAtUtc
                    && exported.AbsoluteExpiresAtUtc == session.AbsoluteExpiresAtUtc
                    && exported.RevokedAtUtc == session.RevokedAtUtc,
                $"sessão criada em {session.CreatedAtUtc:O} ausente ou diferente no export");
        }

        export.Sections.Keys.ShouldBe(["follows"], "a seção que devolve null é omitida");
        export.Sections["follows"].ShouldBeSameAs(follows);
        followsSection.Requested.ShouldBe([user.Id]);
        emptySection.Requested.ShouldBe([user.Id]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:UT-02")]
    public async Task ExportFileHasNoHashNoTokenAndNothingFromAnotherUser()
    {
        var harness = new AccountHarness(new FixedPersonalDataSection("follows", new { bands = "x" }));
        var (signedIn, firstToken) = await harness.Base.SignInAsync(Email);
        var (_, secondToken) = await harness.Base.SignInAsync(Email);
        var (other, otherToken) = await harness.Base.SignInAsync(OtherEmail);

        var result = await harness.ExportAsync(signedIn.UserId);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var json = JsonSerializer.Serialize(result.Value, JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(json);
        var names = PropertyNames(document.RootElement).ToList();
        names.ShouldNotContain(
            name => name.Contains("hash", StringComparison.OrdinalIgnoreCase)
                || name.Contains("token", StringComparison.OrdinalIgnoreCase),
            $"campo de hash ou token no export: {string.Join(", ", names)}");
        foreach (var session in harness.Base.Sessions.All)
        {
            json.ShouldNotContain(session.IdHash, Case.Insensitive, "hash de sessão no export");
        }

        foreach (var token in new[] { firstToken, secondToken, otherToken })
        {
            json.ShouldNotContain(token, Case.Sensitive, "token de sessão no export");
        }

        json.ShouldNotContain(OtherEmail, Case.Insensitive, "e-mail de outro usuário no export");
        json.ShouldNotContain(other.UserId.ToString(), Case.Insensitive, "id de outro usuário no export");
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
            JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
            _ => [],
        };
}
