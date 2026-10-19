using System.Reflection;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.ListProfileShows;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0026 UT-02 — o filtro de shows por perfil (<c>GET /api/v1/shows?profileId=&amp;period=</c>, §6), no handler com
/// a leitura de shows num dublê que registra a consulta: dado o perfil organizador de um show, casa de outro e banda de
/// um terceiro (o que a leitura devolve, na ordem dela), os três entram; <c>upcoming</c> pede os próximos (a partir de
/// "agora", com a tolerância de até 6 h da agenda) até 50, por data crescente; <c>past</c> pede os passados até 20, mais
/// recentes primeiro; outro <c>period</c> → <c>events.invalid_query</c> sem consultar o banco. A parte do SQL (as três
/// colunas: organizador, casa e line-up) é conferida no IT-02, com o PostgreSQL real.
/// </summary>
public sealed class ListProfileShowsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Profile = Guid.CreateVersion7();

    private static ShowSummaryResponse Summary(string title, int days) => new(
        Guid.CreateVersion7(),
        $"{title.ToLowerInvariant().Replace(' ', '-')}-{Now.AddDays(days):yyyy-MM-dd}",
        title,
        Now.AddDays(days).UtcDateTime,
        "Porão do Rock",
        ["Podridão"],
        ["death"],
        ShowStatus.Scheduled,
        null,
        0);

    [Fact]
    [Trait("Spec", "SPEC-0026:UT-02")]
    public async Task UpcomingAsksForTheNextFiftyFromNowAndKeepsTheThreeRolesInDateOrder()
    {
        IReadOnlyList<ShowSummaryResponse> involved =
        [
            Summary("Como Organizador", 3),
            Summary("Como Casa", 5),
            Summary("Como Banda", 9),
        ];
        var shows = RecordingShows.Returning(involved);
        var handler = new ListProfileShowsQueryHandler(shows.Proxy, new FakeTimeProvider(Now));

        var result = await handler.Handle(new ListProfileShowsQuery(Profile, "upcoming"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Shows.Select(show => show.Title).ShouldBe(["Como Organizador", "Como Casa", "Como Banda"]);
        var call = shows.Calls.ShouldHaveSingleItem("a leitura de shows por perfil não foi consultada (ou foi mais de uma vez)");
        call.ProfileId.ShouldBe(Profile);
        call.Upcoming.ShouldBeTrue("period=upcoming pediu os passados");
        call.Limit.ShouldBe(50);
        call.BoundaryUtc.ShouldBeInRange(Now - TimeSpan.FromHours(6), Now, "o corte dos próximos não é \"agora\" (com a tolerância da agenda)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0026:UT-02")]
    public async Task PastAsksForTheLastTwentyMostRecentFirst()
    {
        IReadOnlyList<ShowSummaryResponse> involved =
        [
            Summary("Banda Ontem", -1),
            Summary("Casa Semana Passada", -7),
            Summary("Organizador Mês Passado", -30),
        ];
        var shows = RecordingShows.Returning(involved);
        var handler = new ListProfileShowsQueryHandler(shows.Proxy, new FakeTimeProvider(Now));

        var result = await handler.Handle(new ListProfileShowsQuery(Profile, "past"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Shows.Select(show => show.Title).ShouldBe(["Banda Ontem", "Casa Semana Passada", "Organizador Mês Passado"]);
        var call = shows.Calls.ShouldHaveSingleItem();
        call.ProfileId.ShouldBe(Profile);
        call.Upcoming.ShouldBeFalse("period=past pediu os próximos");
        call.Limit.ShouldBe(20);
        call.BoundaryUtc.ShouldBeInRange(Now - TimeSpan.FromHours(6), Now);
    }

    [Theory]
    [Trait("Spec", "SPEC-0026:UT-02")]
    [InlineData("amanha")]
    [InlineData("UPCOMING")]
    public async Task AnotherPeriodIsInvalidQueryWithoutReadingTheShows(string period)
    {
        var shows = RecordingShows.Returning([]);
        var handler = new ListProfileShowsQueryHandler(shows.Proxy, new FakeTimeProvider(Now));

        var result = await handler.Handle(new ListProfileShowsQuery(Profile, period), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue($"period=\"{period}\" foi aceito");
        result.Error.Code.ShouldBe("events.invalid_query");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        shows.Calls.ShouldBeEmpty("o banco foi consultado com um período inválido");
    }

    internal sealed record ProfileCall(Guid ProfileId, DateTimeOffset BoundaryUtc, bool Upcoming, int Limit);

    /// <summary>
    /// Leitura de shows que registra <see cref="IShowsReadModel.ListByProfileAsync"/> e devolve a lista configurada (até o
    /// limite pedido, como o banco faria); as outras consultas não são usadas aqui.
    /// </summary>
    public class RecordingShows : DispatchProxy
    {
        private IReadOnlyList<ShowSummaryResponse> _shows = [];

        internal List<ProfileCall> Calls { get; } = [];

        internal static RecordingShowsHandle Returning(IReadOnlyList<ShowSummaryResponse> shows)
        {
            var proxy = Create<IShowsReadModel, RecordingShows>();
            var recording = (RecordingShows)(object)proxy;
            recording._shows = shows;
            return new RecordingShowsHandle(proxy, recording);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);
            if (targetMethod.Name != nameof(IShowsReadModel.ListByProfileAsync))
            {
                throw new NotSupportedException($"{targetMethod.Name} não é usado pela lista de shows do perfil.");
            }

            var limit = (int)args[3]!;
            Calls.Add(new ProfileCall((Guid)args[0]!, (DateTimeOffset)args[1]!, (bool)args[2]!, limit));
            return Task.FromResult<IReadOnlyList<ShowSummaryResponse>>([.. _shows.Take(limit)]);
        }
    }

    /// <summary>O dublê como <see cref="IShowsReadModel"/> e as chamadas que ele registrou.</summary>
    internal sealed record RecordingShowsHandle(IShowsReadModel Proxy, RecordingShows Recording)
    {
        public IReadOnlyList<ProfileCall> Calls => Recording.Calls;
    }
}
