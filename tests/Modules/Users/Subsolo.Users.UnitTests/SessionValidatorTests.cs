using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0014 UT-03 — <c>ISessionValidator.ValidateAsync</c> devolve <see langword="null"/> para sessão revogada,
/// expirada (30 dias sem uso), inexistente e para a sessão usada todo dia mas criada há 90 dias e 1 minuto; para a
/// sessão válida, devolve o usuário e os papéis atuais. UT-04 — renovação do prazo deslizante: usada há 59 min não
/// muda <c>expires_at</c>; há 61 min passa a <c>agora + 30 dias</c>; perto do teto, passa ao teto
/// (<c>criada + 90 dias</c>), nunca a <c>agora + 30 dias</c>.
/// </summary>
public sealed class SessionValidatorTests
{
    private static readonly TimeSpan Sliding = TimeSpan.FromDays(30);
    private static readonly TimeSpan Absolute = TimeSpan.FromDays(90);

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-03")]
    public async Task ValidSessionReturnsTheUserAndTheCurrentRoles()
    {
        var harness = new UsersHarness();
        harness.Options.AdminEmails = [];
        var (result, sessionId) = await harness.SignInAsync("fa@subsolo.test");

        var principal = await harness.ValidateAsync(sessionId);

        principal.ShouldNotBeNull("sessão recém-criada é válida");
        principal.UserId.ShouldBe(result.UserId);
        principal.Roles.ShouldBe([UserRoles.Fan]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-03")]
    public async Task RolesComeFromTheUserNotFromTheSessionCreation()
    {
        var harness = new UsersHarness();
        harness.Options.AdminEmails = [];
        var (result, firstSession) = await harness.SignInAsync("fa@subsolo.test");

        // Outra entrada, já com o e-mail na lista de admins: o papel muda no usuário, e a sessão antiga passa a vê-lo.
        harness.Options.AdminEmails = ["fa@subsolo.test"];
        await harness.SignInAsync("fa@subsolo.test");

        var principal = await harness.ValidateAsync(firstSession);

        principal.ShouldNotBeNull();
        principal.UserId.ShouldBe(result.UserId);
        principal.Roles.ShouldBe([UserRoles.Fan, UserRoles.Admin], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-03")]
    public async Task UnknownSessionReturnsNull()
    {
        var harness = new UsersHarness();
        await harness.SignInAsync("fa@subsolo.test");

        (await harness.ValidateAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-03")]
    public async Task RevokedSessionReturnsNull()
    {
        var harness = new UsersHarness();
        var (result, sessionId) = await harness.SignInAsync("fa@subsolo.test");
        harness.SessionOf(result.UserId).Revoke(harness.Clock.GetUtcNow());

        (await harness.ValidateAsync(sessionId)).ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-03")]
    public async Task SessionUnusedFor30DaysReturnsNull()
    {
        var harness = new UsersHarness();
        var (_, sessionId) = await harness.SignInAsync("fa@subsolo.test");

        harness.Clock.Advance(Sliding + TimeSpan.FromMinutes(1));

        (await harness.ValidateAsync(sessionId)).ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-03")]
    public async Task SessionUsedEveryDayExpiresNinetyDaysAfterTheSignIn()
    {
        var harness = new UsersHarness();
        var (_, sessionId) = await harness.SignInAsync("fa@subsolo.test");

        for (var day = 1; day < 90; day++)
        {
            harness.Clock.Advance(TimeSpan.FromDays(1));
            (await harness.ValidateAsync(sessionId)).ShouldNotBeNull($"usada todo dia, a sessão vale no dia {day}");
        }

        harness.Clock.SetUtcNow(UsersHarness.Start + Absolute + TimeSpan.FromMinutes(1));

        (await harness.ValidateAsync(sessionId)).ShouldBeNull("90 dias e 1 minuto depois da entrada, a sessão acabou");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-04")]
    public async Task SignInStartsThe30DaySlidingAndThe90DayAbsoluteExpiration()
    {
        var harness = new UsersHarness();
        var (result, _) = await harness.SignInAsync("fa@subsolo.test");

        var session = harness.SessionOf(result.UserId);

        session.CreatedAtUtc.ShouldBe(UsersHarness.Start);
        session.LastSeenAtUtc.ShouldBe(UsersHarness.Start);
        session.ExpiresAtUtc.ShouldBe(UsersHarness.Start + Sliding);
        session.AbsoluteExpiresAtUtc.ShouldBe(UsersHarness.Start + Absolute);
        session.RevokedAtUtc.ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-04")]
    public async Task UseWithinAnHourDoesNotRenewNorWrite()
    {
        var harness = new UsersHarness();
        var (result, sessionId) = await harness.SignInAsync("fa@subsolo.test");
        var saves = harness.UnitOfWork.Saves;

        harness.Clock.Advance(TimeSpan.FromMinutes(59));
        (await harness.ValidateAsync(sessionId)).ShouldNotBeNull();

        var session = harness.SessionOf(result.UserId);
        session.ExpiresAtUtc.ShouldBe(UsersHarness.Start + Sliding);
        session.LastSeenAtUtc.ShouldBe(UsersHarness.Start);
        harness.UnitOfWork.Saves.ShouldBe(saves, "uso antes de 1 h não grava");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-04")]
    public async Task UseAfterAnHourRenewsTo30DaysFromNow()
    {
        var harness = new UsersHarness();
        var (result, sessionId) = await harness.SignInAsync("fa@subsolo.test");
        var saves = harness.UnitOfWork.Saves;

        harness.Clock.Advance(TimeSpan.FromMinutes(61));
        (await harness.ValidateAsync(sessionId)).ShouldNotBeNull();

        var now = harness.Clock.GetUtcNow();
        var session = harness.SessionOf(result.UserId);
        session.ExpiresAtUtc.ShouldBe(now + Sliding);
        session.LastSeenAtUtc.ShouldBe(now);
        session.AbsoluteExpiresAtUtc.ShouldBe(UsersHarness.Start + Absolute, "o teto não se move");
        harness.UnitOfWork.Saves.ShouldBe(saves + 1, "a renovação é gravada uma vez");
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:UT-04")]
    public async Task RenewalNearTheCeilingStopsAtNinetyDaysFromTheSignIn()
    {
        var harness = new UsersHarness();
        var (result, sessionId) = await harness.SignInAsync("fa@subsolo.test");
        var ceiling = UsersHarness.Start + Absolute;

        // Mantém a sessão viva (uso a cada 25 dias) até 75 dias depois da entrada.
        foreach (var day in new[] { 25, 50, 75 })
        {
            harness.Clock.SetUtcNow(UsersHarness.Start + TimeSpan.FromDays(day));
            (await harness.ValidateAsync(sessionId)).ShouldNotBeNull($"sessão usada no dia {day}");
        }

        var session = harness.SessionOf(result.UserId);
        session.ExpiresAtUtc.ShouldBe(ceiling, "no dia 75, agora + 30 dias passaria do teto: a renovação para no teto");

        // Criada há 75 dias e usada há 61 min: renova, mas continua no teto (não vai a agora + 30 dias).
        harness.Clock.Advance(TimeSpan.FromMinutes(61));
        (await harness.ValidateAsync(sessionId)).ShouldNotBeNull();

        session.ExpiresAtUtc.ShouldBe(ceiling);
        session.ExpiresAtUtc.ShouldNotBe(harness.Clock.GetUtcNow() + Sliding);
    }
}
