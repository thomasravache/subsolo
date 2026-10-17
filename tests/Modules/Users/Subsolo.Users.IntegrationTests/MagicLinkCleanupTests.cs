using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Quartz;
using Subsolo.Common.Application.Messaging;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Users.Application.Features.CleanupMagicLinkTokens;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-06 (ADR-0008) — dados um link usado e um expirado há 25 h e um válido, quando a limpeza roda, só o
/// válido continua em <c>users.magic_link_tokens</c>; a limpeza está agendada como tarefa periódica do host.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MagicLinkCleanupTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    [Fact]
    [Trait("Spec", "SPEC-0016:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task CleanupDeletesUsedAndExpiredTokensOlderThan24HoursAndKeepsTheValidOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Now());
        var (api, database) = await MagicLinkHost.StartAsync(postgres, mailpit, ct, clock);
        await using var _ = api;
        using var client = api.CreateUsersClient();

        var used = await client.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("usado"), ct);
        using (var consumed = await client.ConsumeMagicLinkAsync(used, ct))
        {
            consumed.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await consumed.Content.ReadAsStringAsync(ct)));
        }

        using var anonymous = api.CreateUsersClient();
        var expired = await anonymous.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("expirado"), ct);
        clock.Advance(TimeSpan.FromHours(25));
        var valid = await anonymous.RequestTokenAsync(mailpit, MagicLinkHost.NewEmail("valido"), ct);
        (await MagicLinkDatabase.RowsAsync(database, ct)).Count.ShouldBe(3);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var cleanup = scope.ServiceProvider.GetRequiredService<ICommandHandler<CleanupMagicLinkTokensCommand>>();
            var result = await cleanup.Handle(new CleanupMagicLinkTokensCommand(), ct);
            result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        }

        var rows = await MagicLinkDatabase.RowsAsync(database, ct);
        var remaining = rows.ShouldHaveSingleItem("só o link válido fica no banco");
        MagicLinkDatabase.RowOf(rows, valid).ShouldBe(remaining);
        MagicLinkDatabase.RowOf(rows, used).ShouldBeNull("o link usado há 25 h foi apagado");
        MagicLinkDatabase.RowOf(rows, expired).ShouldBeNull("o link expirado há 25 h foi apagado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task CleanupIsScheduledAsAPeriodicJob()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await MagicLinkHost.StartAsync(postgres, mailpit, ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();
        using (await client.GetAsync("/health", ct))
        {
            // Sobe o host (e o agendador do Quartz) antes de olhar os jobs.
        }

        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        var jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);

        jobs.Select(job => job.Name).ShouldContain(
            name => name.Contains("MagicLink", StringComparison.OrdinalIgnoreCase),
            $"nenhuma tarefa periódica de limpeza dos links agendada (jobs: {string.Join(", ", jobs.Select(job => job.Name))})");
    }

    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
    }
}
