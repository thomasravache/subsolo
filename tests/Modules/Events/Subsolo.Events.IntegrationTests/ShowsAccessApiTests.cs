using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 IT-03 — quem cria um show do perfil de produtor P (de um dono) e do perfil sem dono S: anônimo 401
/// <c>auth.required</c>, fã 403 <c>auth.forbidden</c>, artista dono de outro perfil 403 <c>auth.forbidden</c> (nada
/// gravado), o dono de P 201 e o admin em S 201. Sem o <c>X-CSRF</c>, 400 <c>security.csrf_invalid</c>. Editar e mudar a
/// situação do show de P seguem a mesma regra (o artista de outro perfil leva 403).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ShowsAccessApiTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0023:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task OnlyTheOwnerOfTheOrganizerAndTheAdminCreateShows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        var (owner, _, producer) = await api.ProducerAsync(admin, "dono.it03@subsolo.test", "Peste Produções", ct);
        var (other, _, _) = await api.ProducerAsync(admin, "outro.it03@subsolo.test", "Caos Produções", ct);
        var (fan, _) = await api.SignedInAsync("fa.it03@subsolo.test", ct);
        using var anonymous = api.CreateFlyersClient();
        using var _ = admin;
        using var __ = owner;
        using var ___ = other;
        using var ____ = fan;
        var ownerless = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct);
        var flyerId = await owner.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 21);

        using (var response = await anonymous.PostJsonAsync(ShowsApi.Shows, ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Anônimo"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using (var response = await fan.PostJsonAsync(ShowsApi.Shows, ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Fã"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }

        using (var response = await other.PostJsonAsync(ShowsApi.Shows, ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Outro Perfil"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }

        using (var response = await owner.SendJsonAsync(
                   HttpMethod.Post, ShowsApi.Shows, ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Sem Csrf"), ct, withCsrf: false))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await ShowsDatabase.CountAsync(api.ConnectionString, ct)).ShouldBe(0, "show gravado por quem não pode");

        var (mine, _) = await owner.CreateShowAsync(ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Do Dono"), ct);
        var (adminShow, _) = await admin.CreateShowAsync(ShowsApi.ShowBody(ownerless, flyerId, startsLocal, title: "Do Admin"), ct);

        var rows = await ShowsDatabase.ShowsAsync(api.ConnectionString, ct);
        rows.Select(row => row.Id).ShouldBe([mine, adminShow], ignoreOrder: true);
        rows.Single(row => row.Id == adminShow).OrganizerProfileId.ShouldBe(ownerless);

        using (var edit = await other.SendJsonAsync(
                   HttpMethod.Put, ShowsApi.Show(mine), ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Do Dono", price: "Grátis"), ct))
        {
            await edit.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }

        using (var status = await other.PostJsonAsync(ShowsApi.Status(mine), new { status = "cancelled" }, ct))
        {
            await status.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }

        using (var status = await anonymous.PostJsonAsync(ShowsApi.Status(mine), new { status = "cancelled" }, ct))
        {
            await status.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        var kept = (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).Single(row => row.Id == mine);
        kept.Price.ShouldBe("R$ 30 / R$ 40 na hora", "a edição negada mudou o show");
        kept.Status.ShouldBe("scheduled", "a mudança de situação negada mudou o show");
    }
}
