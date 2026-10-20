using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0027 — os handlers e a interface pública do módulo Engagement sobre dublês em memória (copiado do
/// <c>ProfilesHarness</c> da SPEC-0021): follows, unidade de trabalho que conta as gravações, o catálogo de cidades de
/// teste (<see cref="TestEventsCatalog"/>: <c>curitiba</c> e <c>sao-paulo</c>), os perfis de teste
/// (<see cref="TestProfilesApi"/>: só os aprovados existem) e o <see cref="FakeTimeProvider"/>. Os tipos são montados
/// pelo contêiner (<see cref="Create{T}"/>), com essas dependências e logs nulos.
/// </summary>
internal sealed class EngagementHarness
{
    public static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public EngagementHarness()
    {
        UnitOfWork = new InMemoryEngagementUnitOfWork(Follows);
        _services = new ServiceCollection()
            .AddSingleton<IFollowRepository>(Follows)
            .AddSingleton<IEngagementUnitOfWork>(UnitOfWork)
            .AddSingleton<IEventsCatalog>(Catalog)
            .AddSingleton<IProfilesApi>(Profiles)
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public InMemoryFollows Follows { get; } = new();

    public InMemoryEngagementUnitOfWork UnitOfWork { get; }

    public TestEventsCatalog Catalog { get; } = new();

    public TestProfilesApi Profiles { get; } = new();

    /// <summary>Um handler (ou a implementação do <c>IEngagementApi</c>) do módulo com as dependências do harness.</summary>
    public T Create<T>() => ActivatorUtilities.CreateInstance<T>(_services);

    /// <summary>Um follow já gravado.</summary>
    public Follow AddFollow(Guid userId, FollowTarget target)
    {
        var follow = Follow.Create(userId, target, Clock.GetUtcNow());
        Follows.AddSaved(follow);
        return follow;
    }
}

/// <summary>
/// Follows em memória: <see cref="Add"/> só entra em <see cref="All"/> depois do <c>SaveChangesAsync</c> (como o EF);
/// <see cref="GetFollowerIdsAsync"/> devolve um id por follow que bate com algum alvo (com repetição, como a consulta).
/// </summary>
internal sealed class InMemoryFollows : IFollowRepository
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

/// <summary>Conta as gravações e grava os follows pendentes.</summary>
internal sealed class InMemoryEngagementUnitOfWork(InMemoryFollows follows) : IEngagementUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        follows.Commit();
        return Task.CompletedTask;
    }
}

/// <summary>Dublê do <see cref="IEventsCatalog"/>: as cidades <c>curitiba</c> e <c>sao-paulo</c>; guarda cada consulta.</summary>
internal sealed class TestEventsCatalog : IEventsCatalog
{
    private readonly HashSet<string> _cities = new(["curitiba", "sao-paulo"], StringComparer.Ordinal);

    public List<string> CityQueries { get; } = [];

    public Task<bool> CityExistsAsync(string slug, CancellationToken ct)
    {
        CityQueries.Add(slug);
        return Task.FromResult(_cities.Contains(slug));
    }

    public Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct) => Task.FromResult<ShowState?>(null);
}

/// <summary>
/// Dublê do <see cref="IProfilesApi"/>: só os perfis aprovados existem (um pedido pendente ainda não é perfil, SPEC-0021);
/// guarda cada consulta.
/// </summary>
internal sealed class TestProfilesApi : IProfilesApi
{
    private readonly Dictionary<Guid, ProfileSummary> _approved = [];

    public List<Guid> Queries { get; } = [];

    /// <summary>Um perfil aprovado de banda (sem dono).</summary>
    public ProfileSummary AddApproved(string name = "Podridão", string slug = "podridao", string type = "band")
    {
        var profile = new ProfileSummary(Guid.CreateVersion7(), type, name, slug, "curitiba", null);
        _approved[profile.Id] = profile;
        return profile;
    }

    public Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProfileSummary>>([]);

    public Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct)
    {
        Queries.Add(profileId);
        return Task.FromResult(_approved.GetValueOrDefault(profileId));
    }
}
