using System.Net;
using Npgsql;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 IT-07 — dado um dono com um show: <c>GET /me/export</c> traz a seção <c>shows</c> com o show criado por ele
/// (e nada de outro usuário); quando o <c>UserDeleted</c> dele é processado (exclusão da conta pela API), o show continua
/// em <c>events.shows</c> e na agenda (é da cena).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ShowsPersonalDataTests(PostgresFixture postgres)
{
    private const string OwnerEmail = "dono.exporta.show@subsolo.test";

    /// <summary>Prazo para a outbox do Users e a inbox dos módulos (a cada 2 s) entregarem o <c>UserDeleted</c>.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task TheExportHasTheShowsSectionAndTheShowsStayAfterTheAccountIsDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var (owner, _, producer) = await api.ProducerAsync(admin, OwnerEmail, "Peste Produções", ct);
        using var __ = owner;
        var ownerless = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct);
        var flyerId = await owner.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 18);
        var (mine, _) = await owner.CreateShowAsync(ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Noite da Peste II"), ct);
        var (others, _) = await admin.CreateShowAsync(ShowsApi.ShowBody(ownerless, flyerId, startsLocal, title: "Show de Outra Pessoa"), ct);

        using (var response = await owner.GetAsync(ShowsApi.Export, ct))
        {
            var export = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            export.GetProperty("sections").TryGetProperty("shows", out var section)
                .ShouldBeTrue($"o export não tem a seção shows: {export.GetProperty("sections")}");
            var text = section.ToString();
            text.ShouldContain(mine.ToString(), Case.Insensitive, $"a seção não traz o show criado pelo usuário: {text}");
            text.ShouldContain("Noite da Peste II", Case.Sensitive, text);
            text.ShouldNotContain(others.ToString(), Case.Insensitive, "a seção traz show de outro usuário");
            text.ShouldNotContain("Show de Outra Pessoa", Case.Sensitive, "a seção traz show de outro usuário");
        }

        using (var delete = await owner.SendJsonAsync(HttpMethod.Delete, ShowsApi.Me, new { confirmEmail = OwnerEmail }, ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, FlyersApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        // O UserDeleted chegou aos módulos quando o perfil do usuário ficou sem dono (SPEC-0021).
        (await Timing.EventuallyAsync(async () => await OwnerOfAsync(api.ConnectionString, producer, ct) is null, DeliveryTimeout, ct))
            .ShouldBeTrue("o UserDeleted não foi processado (o perfil continua com dono)");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).Select(row => row.Id).ShouldContain(mine, "o show do usuário excluído foi apagado");
        (await admin.AgendaShowsAsync(ShowsApi.City, ct)).ById(mine).ShouldNotBeNull("o show do usuário excluído saiu da agenda");
    }

    private static async Task<Guid?> OwnerOfAsync(string connectionString, Guid profileId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("select owner_user_id from profiles.profiles where id = @id", connection);
        command.Parameters.AddWithValue("id", profileId);
        var value = await command.ExecuteScalarAsync(ct);
        return value is Guid owner ? owner : null;
    }
}
