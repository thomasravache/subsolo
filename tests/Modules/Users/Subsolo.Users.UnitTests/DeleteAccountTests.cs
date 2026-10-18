using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0019 UT-03 — o handler de <c>DeleteAccount</c>: com <c>confirmEmail</c> " FULANO@email.com " para a conta
/// <c>fulano@email.com</c>, remove o usuário, as sessões dele e os links mágicos do e-mail (os dos outros ficam) e gera
/// <see cref="UserDeletedDomainEvent"/> só com o id; com outro e-mail, <c>users.delete_confirmation_mismatch</c> (ligado
/// ao campo <c>confirmEmail</c>) e nada é removido.
/// </summary>
public sealed class DeleteAccountTests
{
    private const string Email = "fulano@email.com";
    private const string OtherEmail = "outra.pessoa@email.com";

    [Fact]
    [Trait("Spec", "SPEC-0019:UT-03")]
    public async Task MatchingEmailWithOtherCaseAndSpacesRemovesTheUserTheSessionsAndTheLinks()
    {
        var harness = new AccountHarness();
        var user = await harness.SignInAsync(Email);
        await harness.SignInAsync(Email);
        var other = await harness.SignInAsync(OtherEmail);
        harness.MagicLinks.Add(MagicLinkToken.Issue(Email, "/conta", harness.Base.Clock.GetUtcNow()).Token);
        harness.MagicLinks.Add(MagicLinkToken.Issue(OtherEmail, "/", harness.Base.Clock.GetUtcNow()).Token);
        var saves = harness.Base.UnitOfWork.Saves;
        var eventsBefore = harness.Base.UnitOfWork.Events.Count;

        var result = await harness.DeleteAsync(user.Id, " FULANO@email.com ");

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        harness.Base.Users.All.ShouldNotContain(candidate => candidate.Id == user.Id, "o usuário não foi removido");
        harness.Base.Users.All.ShouldContain(candidate => candidate.Id == other.Id, "o outro usuário foi removido");
        harness.Base.Sessions.All.ShouldNotContain(session => session.UserId == user.Id, "sobrou sessão do usuário");
        harness.Base.Sessions.All.ShouldContain(session => session.UserId == other.Id, "a sessão do outro usuário foi removida");
        harness.MagicLinks.All.ShouldNotContain(token => token.Email == Email, "sobrou link mágico do e-mail");
        harness.MagicLinks.All.ShouldContain(token => token.Email == OtherEmail, "o link do outro e-mail foi removido");
        harness.Base.UnitOfWork.Saves.ShouldBe(saves + 1, "a exclusão é gravada numa só transação");

        var deleted = harness.Base.UnitOfWork.Events.Skip(eventsBefore).OfType<UserDeletedDomainEvent>()
            .ShouldHaveSingleItem("UserDeletedDomainEvent não foi gerado (ou foi gerado mais de uma vez)");
        deleted.UserId.ShouldBe(user.Id);
        typeof(UserDeletedDomainEvent).GetProperties().Select(property => property.Name)
            .ShouldBe(["UserId", "Id", "OccurredOnUtc"], ignoreOrder: true, "o evento leva só o id (e a identidade/data do evento)");
    }

    [Theory]
    [Trait("Spec", "SPEC-0019:UT-03")]
    [InlineData("outra.pessoa@email.com")]
    [InlineData("fulano@email.co")]
    [InlineData("")]
    [InlineData(null)]
    public async Task OtherEmailIsAMismatchAndRemovesNothing(string? confirmEmail)
    {
        var harness = new AccountHarness();
        var user = await harness.SignInAsync(Email);
        harness.MagicLinks.Add(MagicLinkToken.Issue(Email, "/conta", harness.Base.Clock.GetUtcNow()).Token);
        var saves = harness.Base.UnitOfWork.Saves;
        var eventsBefore = harness.Base.UnitOfWork.Events.Count;

        var result = await harness.DeleteAsync(user.Id, confirmEmail);

        result.IsFailure.ShouldBeTrue($"\"{confirmEmail}\" não deveria confirmar a exclusão");
        result.Error.Code.ShouldBe(UsersErrors.DeleteConfirmationMismatch);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors.Keys.ShouldContain("confirmEmail", "o erro não está ligado ao campo confirmEmail");
        harness.Base.Users.All.ShouldContain(candidate => candidate.Id == user.Id);
        harness.Base.Sessions.All.ShouldContain(session => session.UserId == user.Id);
        harness.MagicLinks.All.ShouldContain(token => token.Email == Email);
        harness.Base.UnitOfWork.Saves.ShouldBe(saves, "nada é gravado");
        harness.Base.UnitOfWork.Events.Skip(eventsBefore).OfType<UserDeletedDomainEvent>().ShouldBeEmpty();
    }
}
