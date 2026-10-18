using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Quartz;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-07 — dado um dono com perfil aprovado e um pedido pendente: <c>GET /me/export</c> traz a seção
/// <c>profiles</c> com os dois (e nada de outro usuário); quando o <c>UserDeleted</c> dele é processado (exclusão da conta
/// pela API), o perfil fica sem dono (e volta a ser reivindicável) e os pedidos dele são apagados. Os pedidos decididos
/// há 91 dias são apagados pela limpeza periódica (<see cref="FakeTimeProvider"/>), e os decididos há menos de 90 dias e
/// os pendentes ficam.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonalDataTests(PostgresFixture postgres)
{
    private const string OwnerEmail = "dono.exporta@subsolo.test";

    /// <summary>Prazo para a outbox do Users e a inbox do Profiles (a cada 2 s) entregarem o <c>UserDeleted</c>.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task TheExportHasTheProfilesSectionWithTheOwnedProfileAndThePendingRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ProfilesApiFactory(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (owner, _) = await api.SignedInAsync(OwnerEmail, ct);
        var (other, _) = await api.SignedInAsync("outra.pessoa.exporta@subsolo.test", ct);
        using var _ = admin;
        using var __ = owner;
        using var ___ = other;
        var profileId = await admin.ApproveAsync(await owner.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct), ct);
        var pendingId = await owner.RequestProfileAsync(ProfilesRequests.Band("Coágulo"), ct);
        var otherRequest = await other.RequestProfileAsync(ProfilesRequests.Band("Banda de Outra Pessoa"), ct);

        using var response = await owner.GetAsync(ProfilesRequests.Export, ct);

        var export = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        export.GetProperty("sections").TryGetProperty("profiles", out var section)
            .ShouldBeTrue($"o export não tem a seção profiles: {export.GetProperty("sections")}");
        var text = section.ToString();
        text.ShouldContain(profileId.ToString(), Case.Insensitive, $"a seção não traz o perfil aprovado: {text}");
        text.ShouldContain("Podridão", Case.Sensitive, text);
        text.ShouldContain(pendingId.ToString(), Case.Insensitive, $"a seção não traz o pedido pendente: {text}");
        text.ShouldContain("Coágulo", Case.Sensitive, text);
        text.ShouldNotContain(otherRequest.ToString(), Case.Insensitive, "a seção traz pedido de outro usuário");
        text.ShouldNotContain("Banda de Outra Pessoa", Case.Insensitive, "a seção traz pedido de outro usuário");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task WhenTheOwnerDeletesTheAccountTheProfileLosesItsOwnerAndTheRequestsAreDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ProfilesApiFactory(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (owner, ownerId) = await api.SignedInAsync(OwnerEmail, ct);
        var (other, otherId) = await api.SignedInAsync("outra.pessoa.fica@subsolo.test", ct);
        using var _ = admin;
        using var __ = owner;
        using var ___ = other;
        var profileId = await admin.ApproveAsync(await owner.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct), ct);
        await owner.RequestProfileAsync(ProfilesRequests.Band("Coágulo"), ct);
        var otherRequest = await other.RequestProfileAsync(ProfilesRequests.Band("Banda de Outra Pessoa"), ct);

        using (var delete = await owner.MutateAsync(HttpMethod.Delete, ProfilesRequests.Me, new { confirmEmail = OwnerEmail }, ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, ProfilesApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        (await Timing.EventuallyAsync(
                async () => (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).All(request => request.RequesterUserId != ownerId),
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("os pedidos do usuário excluído não foram apagados");
        (await Timing.EventuallyAsync(
                async () => (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Single(profile => profile.Id == profileId).OwnerUserId is null,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("o perfil do usuário excluído continua com dono");

        var profiles = await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct);
        profiles.ShouldContain(profile => profile.Id == profileId, "o perfil fica (o histórico de shows é da cena)");
        var requests = await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct);
        requests.ShouldContain(request => request.Id == otherRequest && request.RequesterUserId == otherId, "o pedido de outra pessoa foi apagado");
        (await other.SearchAsync("pod", ct)).ById("id", profileId).ShouldNotBeNull("o perfil sem dono volta para a busca de reivindicação");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task TheCleanupDeletesRequestsDecided91DaysAgoAndKeepsRecentAndPendingOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ProfilesApiFactory(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (rejected, _) = await api.SignedInAsync("fa.recusado.antigo@subsolo.test", ct);
        var (approved, _) = await api.SignedInAsync("fa.aprovado.antigo@subsolo.test", ct);
        using var _ = admin;
        using var __ = rejected;
        using var ___ = approved;
        var oldRejected = await rejected.RequestProfileAsync(ProfilesRequests.Band("Banda Recusada Antiga"), ct);
        await admin.RejectAsync(oldRejected, "Pedido antigo recusado.", ct);
        var oldApproved = await approved.RequestProfileAsync(ProfilesRequests.Band("Banda Aprovada Antiga"), ct);
        var profileId = await admin.ApproveAsync(oldApproved, ct);

        clock.Advance(TimeSpan.FromDays(60));
        var (recentAdmin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (recent, _) = await api.SignedInAsync("fa.recusado.recente@subsolo.test", ct);
        var (pending, _) = await api.SignedInAsync("fa.pendente@subsolo.test", ct);
        using var ____ = recentAdmin;
        using var _____ = recent;
        using var ______ = pending;
        var recentRejected = await recent.RequestProfileAsync(ProfilesRequests.Band("Banda Recusada Recente"), ct);
        await recentAdmin.RejectAsync(recentRejected, "Pedido recente recusado.", ct);
        var stillPending = await pending.RequestProfileAsync(ProfilesRequests.Band("Banda Pendente"), ct);

        clock.Advance(TimeSpan.FromDays(31));
        await RunCleanupAsync(api, ct);

        (await Timing.EventuallyAsync(
                async () => (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).All(request => request.Id != oldRejected && request.Id != oldApproved),
                TimeSpan.FromSeconds(15),
                ct))
            .ShouldBeTrue("os pedidos decididos há 91 dias não foram apagados");
        var remaining = (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).Select(request => request.Id).ToList();
        remaining.ShouldContain(recentRejected, "o pedido decidido há 31 dias ficou");
        remaining.ShouldContain(stillPending, "o pedido pendente ficou");
        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).ShouldContain(
            profile => profile.Id == profileId, "apagar o pedido não apaga o perfil aprovado");
    }

    /// <summary>Dispara a tarefa periódica de limpeza dos pedidos do módulo Profiles (um job do Quartz agendado no host).</summary>
    private static async Task RunCleanupAsync(ProfilesApiFactory api, CancellationToken ct)
    {
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        var jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);
        var cleanup = jobs.Where(job =>
                job.Name.Contains("Profile", StringComparison.OrdinalIgnoreCase)
                && job.Name.Contains("Cleanup", StringComparison.OrdinalIgnoreCase))
            .ToList();
        cleanup.ShouldHaveSingleItem(
            $"nenhuma (ou mais de uma) tarefa periódica de limpeza dos pedidos agendada (jobs: {string.Join(", ", jobs.Select(job => job.Name))})");
        await scheduler.TriggerJob(cleanup[0], new JobDataMap(), ct);
    }
}
