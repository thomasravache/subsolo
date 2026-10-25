using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.GetAttendees;
using Subsolo.Modules.Engagement.Application.Features.GetFollowers;
using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0027 — os handlers e a interface pública do módulo Engagement sobre dublês em memória (copiado do
/// <c>ProfilesHarness</c> da SPEC-0021): follows, unidade de trabalho que conta as gravações, o catálogo de cidades de
/// teste (<see cref="TestEventsCatalog"/>: <c>curitiba</c> e <c>sao-paulo</c>), os perfis de teste
/// (<see cref="TestProfilesApi"/>: só os aprovados existem) e o <see cref="FakeTimeProvider"/>. Os tipos são montados
/// pelo contêiner (<see cref="Create{T}"/>), com essas dependências e logs nulos. SPEC-0029: os "vou" em memória
/// (<see cref="Attendances"/>) e os shows de teste do catálogo (<see cref="TestEventsCatalog.AddShow"/>); os follows
/// também servem de read model (<see cref="IFollowsReadModel"/>) e o catálogo dá os nomes das cidades
/// (<see cref="IEventsCatalogNames"/>). SPEC-0031: o resumo dos shows do catálogo (<see cref="TestEventsCatalog.AddSummary"/>),
/// o <see cref="IEngagementApi"/> do módulo sobre esses follows e perfis, o registro dos shows publicados em memória
/// (<see cref="PublishedShows"/>) e o <see cref="INotificationsApi"/> que só guarda os avisos enfileirados
/// (<see cref="Notifications"/>). SPEC-0032: os "vou" também servem de read model (<see cref="IAttendancesReadModel"/>), o
/// <see cref="IAttendanceApi"/> do módulo sobre eles, e o catálogo lista os shows agendados por início
/// (<see cref="TestEventsCatalog.ListScheduledShowsStartingAsync"/>) e lê situações em lote
/// (<see cref="TestEventsCatalog.GetShowStatesAsync"/>).
/// </summary>
internal sealed class EngagementHarness
{
    public static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public EngagementHarness()
    {
        UnitOfWork = new InMemoryEngagementUnitOfWork(Follows, Attendances);
        _services = new ServiceCollection()
            .AddSingleton<IFollowRepository>(Follows)
            .AddSingleton<IFollowsReadModel>(Follows)
            .AddSingleton<IAttendanceRepository>(Attendances)
            .AddSingleton<IAttendancesReadModel>(Attendances)
            .AddSingleton<IAttendanceApi>(provider => ActivatorUtilities.CreateInstance<AttendanceApi>(provider))
            .AddSingleton<IEventsCatalogNames>(Catalog)
            .AddSingleton<IEngagementUnitOfWork>(UnitOfWork)
            .AddSingleton<IEventsCatalog>(Catalog)
            .AddSingleton<IProfilesApi>(Profiles)
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<IPublishedShows>(PublishedShows)
            .AddSingleton<INotificationsApi>(Notifications)
            .AddSingleton<IEngagementApi>(provider => ActivatorUtilities.CreateInstance<EngagementApi>(provider))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public InMemoryFollows Follows { get; } = new();

    public InMemoryAttendances Attendances { get; } = new();

    public InMemoryEngagementUnitOfWork UnitOfWork { get; }

    public TestEventsCatalog Catalog { get; } = new();

    public TestProfilesApi Profiles { get; } = new();

    /// <summary>SPEC-0031 — <c>engagement.published_shows</c> em memória.</summary>
    public InMemoryPublishedShows PublishedShows { get; } = new();

    /// <summary>SPEC-0031 — os avisos enfileirados no Notifications.</summary>
    public CapturingNotificationsApi Notifications { get; } = new();

    /// <summary>Um handler (ou a implementação do <c>IEngagementApi</c>) do módulo com as dependências do harness.</summary>
    public T Create<T>() => ActivatorUtilities.CreateInstance<T>(_services);

    /// <summary>Um follow já gravado.</summary>
    public Follow AddFollow(Guid userId, FollowTarget target)
    {
        var follow = Follow.Create(userId, target, Clock.GetUtcNow());
        Follows.AddSaved(follow);
        return follow;
    }

    /// <summary>SPEC-0029 — um "vou" já gravado.</summary>
    public Attendance AddAttendance(Guid userId, Guid showId)
    {
        var attendance = Attendance.Create(userId, showId, Clock.GetUtcNow());
        Attendances.AddSaved(attendance);
        return attendance;
    }
}

/// <summary>
/// Follows em memória: <see cref="Add"/> só entra em <see cref="All"/> depois do <c>SaveChangesAsync</c> (como o EF);
/// <see cref="GetFollowerIdsAsync"/> devolve um id por follow que bate com algum alvo (com repetição, como a consulta).
/// </summary>
internal sealed class InMemoryFollows : IFollowRepository, IFollowsReadModel
{
    private readonly List<Follow> _saved = [];
    private readonly List<Follow> _pending = [];

    public IReadOnlyList<Follow> All => _saved;

    public Task<bool> ExistsAsync(Guid userId, FollowTarget target, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.Any(follow => follow.UserId == userId && follow.Target == target));

    public void Add(Follow follow) => _pending.Add(follow);

    public Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(IReadOnlyCollection<FollowTarget> targets, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            [.. _saved.Where(follow => targets.Contains(follow.Target)).Select(follow => follow.UserId)]);

    public void AddSaved(Follow follow) => _saved.Add(follow);

    public Task<IReadOnlyList<Follow>> ListOfAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Follow>>([.. _saved.Where(follow => follow.UserId == userId)]);

    public Task<int> CountAsync(FollowTarget target, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.Count(follow => follow.Target == target));

    /// <summary>Grava os pendentes; um segundo follow do mesmo usuário e alvo violaria a chave primária.</summary>
    public void Commit()
    {
        foreach (var follow in _pending)
        {
            if (_saved.Any(saved => saved.UserId == follow.UserId && saved.Target == follow.Target))
            {
                throw new InvalidOperationException(
                    $"chave primária violada: {follow.UserId} já segue {follow.TargetKind}:{follow.TargetKey}");
            }

            _saved.Add(follow);
        }

        _pending.Clear();
    }
}

/// <summary>
/// SPEC-0029 — "vou" em memória: <see cref="Add"/> só entra em <see cref="All"/> depois do <c>SaveChangesAsync</c>
/// (como o EF); um segundo "vou" do mesmo usuário no mesmo show violaria a chave primária.
/// </summary>
internal sealed class InMemoryAttendances : IAttendanceRepository, IAttendancesReadModel
{
    private readonly List<Attendance> _saved = [];
    private readonly List<Attendance> _pending = [];

    public IReadOnlyList<Attendance> All => _saved;

    public Task<bool> ExistsAsync(Guid userId, Guid showId, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.Any(attendance => attendance.UserId == userId && attendance.ShowId == showId));

    public void Add(Attendance attendance) => _pending.Add(attendance);

    public Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            _saved.Where(attendance => showIds.Contains(attendance.ShowId))
                .GroupBy(attendance => attendance.ShowId)
                .ToDictionary(group => group.Key, group => group.Count()));

    public Task<IReadOnlyList<Guid>> GetAttendeeIdsAsync(Guid showId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. _saved.Where(attendance => attendance.ShowId == showId).Select(attendance => attendance.UserId)]);

    /// <summary>SPEC-0038 (H36-02) — cada leitura em lote de <see cref="GetAttendeeIdsAsync(IReadOnlyCollection{Guid}, CancellationToken)"/> (os ids pedidos).</summary>
    public List<IReadOnlyCollection<Guid>> AttendeesQueries { get; } = [];

    /// <summary>SPEC-0036 (H32-04) — os ids de quem marcou "vou" em cada show pedido (sem "vou", fora do dicionário).</summary>
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetAttendeeIdsAsync(
        IReadOnlyCollection<Guid> showIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        AttendeesQueries.Add([.. showIds]);
        return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
            _saved.Where(attendance => showIds.Contains(attendance.ShowId))
                .GroupBy(attendance => attendance.ShowId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<Guid>)[.. group.Select(attendance => attendance.UserId)]));
    }

    public void AddSaved(Attendance attendance) => _saved.Add(attendance);

    /// <summary>SPEC-0032 — os "vou" de <paramref name="userId"/>, do mais antigo para o mais novo.</summary>
    public Task<IReadOnlyList<Attendance>> ListOfAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Attendance>>(
            [.. _saved.Where(attendance => attendance.UserId == userId).OrderBy(attendance => attendance.CreatedAtUtc)]);

    /// <summary>Grava os pendentes; um segundo "vou" do mesmo usuário e show violaria a chave primária.</summary>
    public void Commit()
    {
        foreach (var attendance in _pending)
        {
            if (_saved.Any(saved => saved.UserId == attendance.UserId && saved.ShowId == attendance.ShowId))
            {
                throw new InvalidOperationException(
                    $"chave primária violada: {attendance.UserId} já marcou vou em {attendance.ShowId}");
            }

            _saved.Add(attendance);
        }

        _pending.Clear();
    }
}

/// <summary>Conta as gravações e grava os follows e os "vou" pendentes.</summary>
internal sealed class InMemoryEngagementUnitOfWork(InMemoryFollows follows, InMemoryAttendances attendances) : IEngagementUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        follows.Commit();
        attendances.Commit();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dublê do <see cref="IEventsCatalog"/>: as cidades <c>curitiba</c> e <c>sao-paulo</c>; guarda cada consulta. SPEC-0029:
/// os shows de teste (<see cref="AddShow"/>, situação e início) e os nomes das cidades (<see cref="IEventsCatalogNames"/>).
/// </summary>
internal sealed class TestEventsCatalog : IEventsCatalog, IEventsCatalogNames
{
    private readonly HashSet<string> _cities = new(["curitiba", "sao-paulo"], StringComparer.Ordinal);
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal) { ["curitiba"] = "Curitiba", ["sao-paulo"] = "São Paulo" };
    private readonly Dictionary<Guid, ShowState> _shows = [];
    private readonly Dictionary<Guid, ShowSummary> _summaries = [];

    public List<Guid> ShowQueries { get; } = [];

    /// <summary>Um show de teste com a situação <paramref name="status"/> e o início <paramref name="startsAtUtc"/>; devolve o id.</summary>
    public Guid AddShow(string status, DateTimeOffset startsAtUtc)
    {
        var id = Guid.CreateVersion7();
        _shows[id] = new ShowState(status, startsAtUtc);
        return id;
    }

    public Task<CatalogNames> GetNamesAsync(
        IReadOnlyCollection<string> citySlugs, IReadOnlyCollection<string> styleSlugs, CancellationToken ct) =>
        Task.FromResult(new CatalogNames(
            citySlugs.Where(_names.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(slug => slug, slug => _names[slug], StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal)));

    public List<string> CityQueries { get; } = [];

    public Task<bool> CityExistsAsync(string slug, CancellationToken ct)
    {
        CityQueries.Add(slug);
        return Task.FromResult(_cities.Contains(slug));
    }

    public Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct)
    {
        ShowQueries.Add(showId);
        return Task.FromResult(_shows.GetValueOrDefault(showId));
    }

    /// <summary>SPEC-0031 — um show de teste com o resumo <paramref name="summary"/> (e a situação e o início dele).</summary>
    public ShowSummary AddSummary(ShowSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        _summaries[summary.ShowId] = summary;
        _shows[summary.ShowId] = new ShowState(summary.Status, summary.StartsAtUtc);
        return summary;
    }

    /// <summary>SPEC-0031 — o resumo do show, ou <see langword="null"/>.</summary>
    public Task<ShowSummary?> GetShowSummaryAsync(Guid showId, CancellationToken ct)
    {
        ShowQueries.Add(showId);
        return Task.FromResult(_summaries.GetValueOrDefault(showId));
    }

    /// <summary>SPEC-0032 (H29-01) — cada leitura em lote de <see cref="GetShowStatesAsync"/> (os ids pedidos).</summary>
    public List<IReadOnlyCollection<Guid>> StatesQueries { get; } = [];

    /// <summary>SPEC-0032 — cada janela pedida a <see cref="ListScheduledShowsStartingAsync"/>.</summary>
    public List<(DateTimeOffset FromUtc, DateTimeOffset ToUtc)> WindowQueries { get; } = [];

    /// <summary>SPEC-0032 (H29-01) — a situação e o início dos shows pedidos, só dos encontrados.</summary>
    public Task<IReadOnlyDictionary<Guid, ShowState>> GetShowStatesAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        StatesQueries.Add([.. showIds]);
        return Task.FromResult<IReadOnlyDictionary<Guid, ShowState>>(
            showIds.Distinct().Where(_shows.ContainsKey).ToDictionary(id => id, id => _shows[id]));
    }

    /// <summary>
    /// SPEC-0032 — os resumos dos shows <c>scheduled</c> com início em [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    public Task<IReadOnlyList<ShowSummary>> ListScheduledShowsStartingAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        WindowQueries.Add((fromUtc, toUtc));
        return Task.FromResult<IReadOnlyList<ShowSummary>>(
        [
            .. _summaries.Values
                .Where(show => show.Status == "scheduled" && show.StartsAtUtc >= fromUtc && show.StartsAtUtc < toUtc)
                .OrderBy(show => show.StartsAtUtc),
        ]);
    }
}

/// <summary>SPEC-0031 — <c>engagement.published_shows</c> em memória (uma linha por show).</summary>
internal sealed class InMemoryPublishedShows : IPublishedShows
{
    private readonly Dictionary<Guid, PublishedShow> _rows = [];

    public IReadOnlyCollection<PublishedShow> All => _rows.Values;

    public Task AddAsync(PublishedShow show, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(show);
        _rows.TryAdd(show.ShowId, show);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PublishedShow>> PublishedSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublishedShow>>([.. _rows.Values.Where(row => row.PublishedAtUtc >= sinceUtc)]);

    public Task<int> DeletePublishedBeforeAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken)
    {
        var old = _rows.Values.Where(row => row.PublishedAtUtc < beforeUtc).Select(row => row.ShowId).ToList();
        foreach (var id in old)
        {
            _rows.Remove(id);
        }

        return Task.FromResult(old.Count);
    }
}

/// <summary>
/// SPEC-0031 — dublê do <see cref="INotificationsApi"/> que só guarda cada <see cref="INotificationsApi.EnqueueAsync"/>
/// (destinatários e aviso); o envio é do Notifications (SPEC-0030).
/// </summary>
internal sealed class CapturingNotificationsApi : INotificationsApi
{
    public List<(IReadOnlyCollection<Guid> UserIds, Notice Notice)> Enqueued { get; } = [];

    public Task<Result> EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken ct)
    {
        Enqueued.Add(([.. userIds], notice));
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("o Engagement não manda e-mail transacional");

    public Task ForgetUserAsync(Guid userId, CancellationToken ct) =>
        throw new InvalidOperationException("o Engagement não apaga dados do Notifications");

    public Task<object?> ExportAsync(Guid userId, CancellationToken ct) =>
        throw new InvalidOperationException("o Engagement não exporta dados do Notifications");
}

/// <summary>
/// Dublê do <see cref="IProfilesApi"/>: só os perfis aprovados existem (um pedido pendente ainda não é perfil, SPEC-0021);
/// guarda cada consulta. SPEC-0029 (H27-06): a busca em lote <see cref="GetManyAsync"/> (assinatura do
/// <c>IShowProfiles.GetManyAsync</c>), com as consultas em <see cref="ManyQueries"/>.
/// </summary>
internal sealed class TestProfilesApi : IProfilesApi
{
    private readonly Dictionary<Guid, ProfileSummary> _approved = [];

    public List<Guid> Queries { get; } = [];

    public List<IReadOnlyCollection<Guid>> ManyQueries { get; } = [];

    /// <summary>Um perfil aprovado de banda (sem dono).</summary>
    public ProfileSummary AddApproved(string name = "Podridão", string slug = "podridao", string type = "band") =>
        AddApproved(Guid.CreateVersion7(), name, slug, type);

    /// <summary>SPEC-0029 — um perfil aprovado com o id <paramref name="id"/>.</summary>
    public ProfileSummary AddApproved(Guid id, string name = "Podridão", string slug = "podridao", string type = "band")
    {
        var profile = new ProfileSummary(id, type, name, slug, "curitiba", null);
        _approved[profile.Id] = profile;
        return profile;
    }

    /// <summary>SPEC-0029 (H27-06) — os perfis aprovados entre <paramref name="profileIds"/>, por id.</summary>
    public Task<IReadOnlyDictionary<Guid, ProfileSummary>> GetManyAsync(IReadOnlyCollection<Guid> profileIds, CancellationToken ct)
    {
        ManyQueries.Add([.. profileIds]);
        return Task.FromResult<IReadOnlyDictionary<Guid, ProfileSummary>>(
            profileIds.Distinct().Where(_approved.ContainsKey).ToDictionary(id => id, id => _approved[id]));
    }

    public Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProfileSummary>>([]);

    public Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct)
    {
        Queries.Add(profileId);
        return Task.FromResult(_approved.GetValueOrDefault(profileId));
    }
}
