using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 IT-04 — dado um show criado pelo dono, ele edita a data e o line-up (<c>PUT /shows/{id}</c>), adia e depois
/// cancela (<c>POST /shows/{id}/status</c>): cada resposta é 200, o slug não muda, a agenda mostra o início novo e a
/// situação certa (<c>postponed</c>, depois <c>cancelled</c>, com o show ainda na agenda) e a outbox tem um
/// <c>ShowChanged</c> por mudança (3). Situação desconhecida → 400 <c>events.invalid_status</c>; show inexistente → 404
/// <c>events.show_not_found</c> (na edição e na situação).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class EditShowApiTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0023:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task EditingPostponingAndCancellingKeepTheSlugShowInTheAgendaAndRaiseOneShowChangedEach()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var (owner, _, producer) = await api.ProducerAsync(admin, "dono.it04@subsolo.test", "Peste Produções", ct);
        using var __ = owner;
        var flyerId = await owner.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 25);
        var (id, slug) = await owner.CreateShowAsync(ShowsApi.ShowBody(producer, flyerId, startsLocal), ct);
        var newStart = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 32, hour: 21, minute: 30);

        using (var edit = await owner.SendJsonAsync(
                   HttpMethod.Put,
                   ShowsApi.Show(id),
                   ShowsApi.ShowBody(producer, flyerId, newStart, lineup: [ShowsApi.Named("Vômito Ácido"), ShowsApi.Named("Cova Rasa"), ShowsApi.Named("Carniça")]),
                   ct))
        {
            var saved = await edit.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            saved.GetProperty("id").GetGuid().ShouldBe(id);
            saved.GetProperty("slug").GetString().ShouldBe(slug, "o slug não muda na edição, mesmo com a data nova");
        }

        var edited = (await admin.AgendaShowsAsync(ShowsApi.City, ct)).ById(id).ShouldNotBeNull("o show editado sumiu da agenda");
        edited.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(ShowsApi.UtcOf(newStart));
        edited.GetProperty("lineup").EnumerateArray().Select(item => item.GetString()).ShouldBe(["Vômito Ácido", "Cova Rasa", "Carniça"]);
        edited.GetProperty("slug").GetString().ShouldBe(slug);

        foreach (var status in new[] { "postponed", "cancelled" })
        {
            using (var change = await owner.PostJsonAsync(ShowsApi.Status(id), new { status }, ct))
            {
                var body = await change.Content.ReadAsStringAsync(ct);
                change.StatusCode.ShouldBe(HttpStatusCode.OK, $"POST {ShowsApi.Status(id)} {status}: {FlyersApiClient.Excerpt(body)}");
            }

            var card = (await admin.AgendaShowsAsync(ShowsApi.City, ct)).ById(id)
                .ShouldNotBeNull($"o show {status} saiu da agenda (ele fica, com o selo)");
            card.GetProperty("status").GetString().ShouldBe(status);
        }

        (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem().Slug.ShouldBe(slug);
        var changes = await ShowsDatabase.OutboxContentsAsync(api.ConnectionString, "ShowChanged", ct);
        changes.Count.ShouldBe(3, $"um ShowChanged por mudança (edição, adiado, cancelado): {string.Join(" | ", changes)}");
        changes.ShouldAllBe(content => content.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase));
        changes[^1].ShouldContain("cancelled", Case.Sensitive);
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task AnUnknownStatusIs400AndAnUnknownShowIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var organizer = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct);
        var flyerId = await admin.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 25);
        var (id, _) = await admin.CreateShowAsync(ShowsApi.ShowBody(organizer, flyerId, startsLocal), ct);
        var missing = Guid.CreateVersion7();

        using (var invalid = await admin.PostJsonAsync(ShowsApi.Status(id), new { status = "sold_out" }, ct))
        {
            await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_status", ct);
        }

        using (var unknown = await admin.PostJsonAsync(ShowsApi.Status(missing), new { status = "cancelled" }, ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }

        using (var unknown = await admin.SendJsonAsync(HttpMethod.Put, ShowsApi.Show(missing), ShowsApi.ShowBody(organizer, flyerId, startsLocal), ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }

        (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem().Status.ShouldBe("scheduled");
        (await ShowsDatabase.OutboxContentsAsync(api.ConnectionString, "ShowChanged", ct)).ShouldBeEmpty();
    }
}
