using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Application.Features.ValidateSession;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Users.UnitTests.Infrastructure;

/// <summary>
/// O <see cref="SignInService"/> e o <see cref="SessionValidator"/> sobre os mesmos dublês em memória: repositórios de
/// usuários e sessões, unidade de trabalho que conta as gravações e recolhe os eventos de domínio (como o interceptor
/// da outbox), emissor de cookie que guarda o token de cada sessão emitida, <see cref="UsersOptions"/> mutável e
/// <see cref="FakeTimeProvider"/>.
/// </summary>
internal sealed class UsersHarness
{
    public const string AdminEmail = "admin@subsolo.test";

    public static readonly DateTimeOffset Start = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    public UsersHarness()
    {
        UnitOfWork = new InMemoryUnitOfWork(Users, Sessions);
        SignIn = new SignInService(
            Users,
            Sessions,
            UnitOfWork,
            Cookies,
            Microsoft.Extensions.Options.Options.Create(Options),
            Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SignInService>.Instance);
        Validator = new SessionValidator(Sessions, Users, UnitOfWork, Clock);
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public UsersOptions Options { get; } = new() { AdminEmails = [AdminEmail] };

    public InMemoryUsers Users { get; } = new();

    public InMemorySessions Sessions { get; } = new();

    public InMemoryUnitOfWork UnitOfWork { get; }

    public CapturingCookieIssuer Cookies { get; } = new();

    public SignInService SignIn { get; }

    public SessionValidator Validator { get; }

    /// <summary>
    /// Entra com <paramref name="email"/> (link mágico), exige sucesso e devolve o token da sessão emitido no cookie
    /// (256 bits em base64url).
    /// </summary>
    public async Task<(SignInResult Result, string SessionToken)> SignInAsync(string email)
    {
        var issued = Cookies.Issued.Count;
        var result = await SignIn.SignInAsync(email, SignInMethod.MagicLink, TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        Cookies.Issued.Count.ShouldBe(issued + 1, "SignInAsync não emitiu o cookie da sessão");
        var cookie = Cookies.Issued[^1];
        cookie.UserId.ShouldBe(result.Value.UserId, "o cookie leva o id do usuário que entrou");
        SessionTokens.ShouldBe256BitBase64Url(cookie.SessionToken);
        return (result.Value, cookie.SessionToken);
    }

    public Task<Subsolo.Modules.Users.IntegrationEvents.SessionPrincipal?> ValidateAsync(string sessionToken) =>
        Validator.ValidateAsync(sessionToken, TestContext.Current.CancellationToken);

    /// <summary>A única sessão gravada para <paramref name="userId"/>.</summary>
    public Session SessionOf(Guid userId) =>
        Sessions.All.Where(session => session.UserId == userId).ShouldHaveSingleItem();
}

internal sealed class InMemoryUsers : IUserRepository
{
    private readonly List<User> _users = [];
    private readonly List<User> _removed = [];

    public IReadOnlyList<User> All => _users;

    /// <summary>SPEC-0019 — usuários apagados (o interceptor da outbox ainda vê os eventos deles na gravação).</summary>
    public IReadOnlyList<User> Removed => _removed;

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(user => user.Email == normalizedEmail));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

    public void Add(User user) => _users.Add(user);

    public void Remove(User user)
    {
        if (_users.Remove(user))
        {
            _removed.Add(user);
        }
    }
}

internal sealed class InMemorySessions : ISessionRepository
{
    private readonly List<Session> _sessions = [];

    public IReadOnlyList<Session> All => _sessions;

    public Task<Session?> GetByIdHashAsync(string idHash, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.FirstOrDefault(session => session.IdHash == idHash));

    public void Add(Session session) => _sessions.Add(session);

    public Task<IReadOnlyList<Session>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Session>>([.. _sessions.Where(session => session.UserId == userId).OrderBy(session => session.CreatedAtUtc)]);

    public void Remove(Session session) => _sessions.Remove(session);
}

/// <summary>Conta as gravações e, como o interceptor da outbox, recolhe (e limpa) os eventos de domínio pendentes.</summary>
internal sealed class InMemoryUnitOfWork(InMemoryUsers users, InMemorySessions sessions) : IUsersUnitOfWork
{
    private readonly List<IDomainEvent> _events = [];

    public int Saves { get; private set; }

    public IReadOnlyList<IDomainEvent> Events => _events;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        foreach (var entity in users.All.Concat(users.Removed).Cast<Entity>().Concat(sessions.All))
        {
            _events.AddRange(entity.DomainEvents);
            entity.ClearDomainEvents();
        }

        return Task.CompletedTask;
    }
}

internal sealed class CapturingCookieIssuer : ISessionCookieIssuer
{
    private readonly List<(string SessionToken, Guid UserId)> _issued = [];

    public IReadOnlyList<(string SessionToken, Guid UserId)> Issued => _issued;

    public Task IssueAsync(string sessionToken, Guid userId, CancellationToken cancellationToken)
    {
        _issued.Add((sessionToken, userId));
        return Task.CompletedTask;
    }
}
