using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0014 UT-01 — duas entradas com o mesmo e-mail (com outra caixa e espaços) dão um único usuário com o e-mail
/// normalizado e o papel <c>fan</c>, duas sessões e <c>UserRegistered</c> só na primeira. UT-02 — e-mail em
/// <c>Users:AdminEmails</c> ganha <c>admin</c>, que sai quando o e-mail deixa a lista; e-mail inválido (sem arroba ou
/// com 255 caracteres) → <c>users.invalid_email</c>, nada gravado.
/// </summary>
public sealed class SignInServiceTests
{
    [Fact]
    [Trait("Spec", "SPEC-0014:UT-01")]
    public async Task SigningInTwiceWithTheSameEmailKeepsASingleFanUserWithTwoSessions()
    {
        var harness = new UsersHarness();

        var (first, firstSession) = await harness.SignInAsync(" Fulano@Email.COM ");
        var (second, secondSession) = await harness.SignInAsync(" Fulano@Email.COM ");

        var user = harness.Users.All.ShouldHaveSingleItem();
        user.Email.ShouldBe("fulano@email.com");
        user.Roles.ShouldBe([UserRoles.Fan]);
        user.Id.ShouldNotBe(Guid.Empty);
        first.UserId.ShouldBe(user.Id);
        second.UserId.ShouldBe(user.Id);
        first.IsNewUser.ShouldBeTrue();
        second.IsNewUser.ShouldBeFalse();

        harness.Sessions.All.Count.ShouldBe(2);
        harness.Sessions.All.ShouldAllBe(session => session.UserId == user.Id && session.Method == SignInMethod.MagicLink);
        firstSession.ShouldNotBe(secondSession, "cada entrada cria uma sessão nova (id novo)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-01")]
    public async Task UserRegisteredIsRaisedOnlyOnTheFirstSignIn()
    {
        var harness = new UsersHarness();

        var (first, _) = await harness.SignInAsync(" Fulano@Email.COM ");
        harness.UnitOfWork.Events.OfType<UserRegisteredDomainEvent>().ShouldHaveSingleItem().UserId.ShouldBe(first.UserId);

        await harness.SignInAsync("fulano@email.com");

        harness.UnitOfWork.Events.OfType<UserRegisteredDomainEvent>().Count().ShouldBe(1, "a segunda entrada não registra o usuário de novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-01")]
    public async Task SessionIdIsStoredOnlyAsAHash()
    {
        var harness = new UsersHarness();

        var (result, sessionId) = await harness.SignInAsync("fulano@email.com");

        var session = harness.SessionOf(result.UserId);
        session.IdHash.ShouldNotBeNullOrWhiteSpace();
        session.IdHash.ShouldNotContain(sessionId.ToString(), Case.Insensitive);
        session.IdHash.ShouldNotContain(sessionId.ToString("N"), Case.Insensitive);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-02")]
    public async Task EmailInAdminEmailsGetsFanAndAdmin()
    {
        var harness = new UsersHarness();

        await harness.SignInAsync(UsersHarness.AdminEmail);

        harness.Users.All.ShouldHaveSingleItem().Roles.ShouldBe([UserRoles.Fan, UserRoles.Admin], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-02")]
    public async Task EmailRemovedFromAdminEmailsLosesAdminOnTheNextSignIn()
    {
        var harness = new UsersHarness();
        await harness.SignInAsync(UsersHarness.AdminEmail);

        harness.Options.AdminEmails = [];
        await harness.SignInAsync(UsersHarness.AdminEmail);

        harness.Users.All.ShouldHaveSingleItem().Roles.ShouldBe([UserRoles.Fan]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-02")]
    public async Task EmailAddedToAdminEmailsGainsAdminOnTheNextSignIn()
    {
        var harness = new UsersHarness();
        harness.Options.AdminEmails = [];
        await harness.SignInAsync(" Admin@Subsolo.TEST ");
        harness.Users.All.ShouldHaveSingleItem().Roles.ShouldBe([UserRoles.Fan]);

        harness.Options.AdminEmails = [UsersHarness.AdminEmail];
        await harness.SignInAsync(UsersHarness.AdminEmail);

        harness.Users.All.ShouldHaveSingleItem().Roles.ShouldBe([UserRoles.Fan, UserRoles.Admin], ignoreOrder: true);
    }

    /// <summary>E-mails inválidos: sem arroba, vazios e com 255 caracteres (242 letras + "@subsolo.test").</summary>
    public static TheoryData<string> InvalidEmails => new()
    {
        "sem-arroba",
        string.Empty,
        "   ",
        new string('a', 242) + "@subsolo.test",
    };

    [Theory]
    [Trait("Spec", "SPEC-0014:UT-02")]
    [MemberData(nameof(InvalidEmails))]
    public async Task InvalidEmailFailsWithInvalidEmailAndWritesNothing(string email)
    {
        var harness = new UsersHarness();

        var result = await harness.SignIn.SignInAsync(email, SignInMethod.MagicLink, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("users.invalid_email");
        harness.Users.All.ShouldBeEmpty();
        harness.Sessions.All.ShouldBeEmpty();
        harness.UnitOfWork.Saves.ShouldBe(0, "nada é gravado com e-mail inválido");
        harness.Cookies.Issued.ShouldBeEmpty("nenhum cookie é emitido com e-mail inválido");
    }
}
