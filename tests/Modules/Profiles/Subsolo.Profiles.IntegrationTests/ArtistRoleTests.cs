using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Subsolo.Common.Application.EventBus;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Users.IntegrationEvents;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-05 — o papel <c>artist</c> chega ao dono pelo evento: com <c>ArtistRoleGranted</c> (contrato do Users, Emenda v2) publicado duas
/// vezes (o mesmo evento) e o processamento da inbox do Users parado, nada muda; quando o processamento volta, o usuário
/// tem o papel <c>artist</c> uma vez só e <c>GET /me</c> mostra <c>artist</c>. De ponta a ponta: aprovar o pedido pela
/// API leva o dono a ter <c>artist</c> em <c>/me</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ArtistRoleTests(PostgresFixture postgres)
{
    /// <summary>O job do Quartz da inbox do Users (<c>{DbContext}.ProcessInbox</c>, SPEC-0007).</summary>
    private static readonly JobKey UsersInbox = new("UsersDbContext.ProcessInbox");

    /// <summary>Prazo para a outbox do Profiles e a inbox do Users (a cada 2 s) entregarem o papel.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task TheEventDeliveredTwiceWhileUsersIsStoppedGivesArtistOnceWhenItResumes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, fanId) = await api.SignedInAsync("fa.artista@subsolo.test", ct);
        using var _ = fan;
        (await fan.RolesAsync(ct)).ShouldNotContain("artist");
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        (await scheduler.GetJobDetail(UsersInbox, ct)).ShouldNotBeNull($"job {UsersInbox} não agendado");
        await scheduler.PauseJob(UsersInbox, ct);
        var integrationEvent = new ArtistRoleGrantedIntegrationEvent(fanId, Guid.CreateVersion7())
        {
            Id = Guid.CreateVersion7(),
            OccurredOnUtc = DateTime.UtcNow,
        };

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(integrationEvent, ct);
            await bus.PublishAsync(integrationEvent, ct);
        }

        var inbox = await ProfilesDatabase.UsersInboxAsync(api.ConnectionString, "ArtistRoleGranted", ct);
        inbox.ShouldHaveSingleItem("o Users não consome ArtistRoleGranted (ou guardou o mesmo evento duas vezes)")
            .Id.ShouldBe(integrationEvent.Id);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        (await fan.RolesAsync(ct)).ShouldNotContain("artist", "com a inbox do Users parada, o papel ainda não chegou");

        await scheduler.ResumeJob(UsersInbox, ct);

        (await Timing.EventuallyAsync(async () => (await fan.RolesAsync(ct)).Contains("artist"), DeliveryTimeout, ct))
            .ShouldBeTrue("o papel artist não chegou depois de a inbox do Users voltar");
        var roles = await fan.RolesAsync(ct);
        roles.Count(role => role == "artist").ShouldBe(1, $"papel artist repetido: {string.Join(", ", roles)}");
        roles.ShouldContain("fan", "o papel fan continua");
        var stored = await ProfilesDatabase.UserRolesAsync(api.ConnectionString, fanId, ct);
        stored.ShouldNotBeNull();
        stored.Split(',', '{', '}', '"').Count(role => role == "artist").ShouldBe(1, $"users.roles = {stored}");
        (await ProfilesDatabase.UsersInboxAsync(api.ConnectionString, "ArtistRoleGranted", ct))
            .ShouldHaveSingleItem().Processed.ShouldBeTrue("a mensagem da inbox foi processada");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task ApprovingTheRequestGivesTheOwnerTheArtistRole()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.vira.artista@subsolo.test", ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = fan;
        using var __ = admin;
        var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);

        await admin.ApproveAsync(requestId, ct);

        (await Timing.EventuallyAsync(async () => (await fan.RolesAsync(ct)).Contains("artist"), DeliveryTimeout, ct))
            .ShouldBeTrue("aprovado o pedido, o dono não ganhou o papel artist");
        (await admin.RolesAsync(ct)).ShouldNotContain("artist", "só o dono vira artista");
    }
}
