using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.ConsumeMagicLink;
using Subsolo.Modules.Users.Application.Features.RequestMagicLink;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Users.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0016 — os handlers <see cref="RequestMagicLinkCommandHandler"/> e <see cref="ConsumeMagicLinkCommandHandler"/>
/// sobre dublês: links em memória (<see cref="InMemoryMagicLinkTokens"/>), o dublê de <see cref="INotificationsApi"/>
/// que guarda cada pedido (<see cref="RecordingNotificationsApi"/>, conferido contra o contrato SPEC-0018@1 no CT-01),
/// o dublê de <see cref="ISignInService"/> e o <see cref="FakeTimeProvider"/>.
/// </summary>
internal sealed class MagicLinkHarness
{
    /// <summary><c>App:PublicUrl</c> dos testes.</summary>
    public const string PublicUrl = "https://subsolo.test";

    /// <summary>Começo do caminho do link mágico no e-mail.</summary>
    public const string ConfirmPrefix = PublicUrl + "/entrar/confirmar?token=";

    public static readonly DateTimeOffset Start = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>SPEC-0021 (Emenda 3) — <paramref name="users"/>: <c>Users</c> com o limite por e-mail (padrão: sem a seção).</summary>
    public MagicLinkHarness(UsersOptions? users = null)
    {
        Request = new RequestMagicLinkCommandHandler(
            Tokens,
            UnitOfWork,
            Notifications,
            Microsoft.Extensions.Options.Options.Create(new PublicUrlOptions { PublicUrl = PublicUrl }),
            Clock,
            NullLogger<RequestMagicLinkCommandHandler>.Instance,
            users: Microsoft.Extensions.Options.Options.Create(users ?? new UsersOptions()));
        Consume = new ConsumeMagicLinkCommandHandler(
            Tokens,
            SignIn,
            Clock,
            NullLogger<ConsumeMagicLinkCommandHandler>.Instance);
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public InMemoryMagicLinkTokens Tokens { get; } = new();

    public SavesCounter UnitOfWork { get; } = new();

    public RecordingNotificationsApi Notifications { get; } = new();

    public RecordingSignInService SignIn { get; } = new();

    public RequestMagicLinkCommandHandler Request { get; }

    public ConsumeMagicLinkCommandHandler Consume { get; }

    public Task<Result> RequestAsync(string email, string? returnTo = null) =>
        Request.Handle(new RequestMagicLinkCommand(email, returnTo), TestContext.Current.CancellationToken);

    public Task<Result<ConsumeMagicLinkResponse>> ConsumeAsync(string token) =>
        Consume.Handle(new ConsumeMagicLinkCommand(token), TestContext.Current.CancellationToken);

    /// <summary>Pede o link para <paramref name="email"/>, exige sucesso com um e-mail enviado e devolve o token do link.</summary>
    public async Task<string> RequestTokenAsync(string email, string? returnTo = null)
    {
        var sent = Notifications.Requests.Count;
        var result = await RequestAsync(email, returnTo);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        Notifications.Requests.Count.ShouldBe(sent + 1, "o pedido do link não mandou o e-mail");
        return TokenOf(Notifications.Requests[^1]);
    }

    /// <summary>O token da URL do link (<c>link</c>) de um pedido de e-mail <see cref="EmailTemplate.MagicLink"/>.</summary>
    public static string TokenOf(EmailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Data.ShouldContainKey("link");
        var link = request.Data["link"];
        link.ShouldStartWith(ConfirmPrefix, Case.Sensitive, $"link fora do formato {{App:PublicUrl}}/entrar/confirmar?token=: {link}");
        var token = HttpUtility.ParseQueryString(new Uri(link).Query)["token"];
        token.ShouldNotBeNullOrWhiteSpace($"link sem token: {link}");
        return token;
    }
}

/// <summary>Links em memória; <see cref="TryMarkUsedAsync"/> usa as regras da própria entidade.</summary>
internal sealed class InMemoryMagicLinkTokens : IMagicLinkTokenRepository
{
    private readonly List<MagicLinkToken> _tokens = [];

    public IReadOnlyList<MagicLinkToken> All => _tokens;

    public void Add(MagicLinkToken token) => _tokens.Add(token);

    public Task<MagicLinkToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task<int> CountIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.Count(token => token.Email == normalizedEmail && token.CreatedAtUtc > sinceUtc));

    public Task<bool> TryMarkUsedAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var token = _tokens.FirstOrDefault(candidate => candidate.TokenHash == tokenHash);
        if (token is null || !token.IsUsable(nowUtc))
        {
            return Task.FromResult(false);
        }

        token.MarkUsed(nowUtc);
        return Task.FromResult(true);
    }

    public Task<int> DeleteStaleAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.RemoveAll(token =>
            token.UsedAtUtc is { } used && used < cutoffUtc || token.ExpiresAtUtc < cutoffUtc));

    public Task<IReadOnlyList<MagicLinkToken>> ListByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MagicLinkToken>>([.. _tokens.Where(token => token.Email == normalizedEmail)]);

    public void Remove(MagicLinkToken token) => _tokens.Remove(token);
}

/// <summary>Unidade de trabalho que só conta as gravações.</summary>
internal sealed class SavesCounter : IUsersUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dublê de <see cref="INotificationsApi"/> (SPEC-0018@1): guarda cada pedido e devolve sucesso, ou a falha com o
/// código de <see cref="NotificationsErrors"/> definido em <see cref="FailWith"/>.
/// </summary>
internal sealed class RecordingNotificationsApi : INotificationsApi
{
    private readonly List<EmailRequest> _requests = [];

    public IReadOnlyList<EmailRequest> Requests => _requests;

    /// <summary>Código de <see cref="NotificationsErrors"/> devolvido nos próximos envios; nulo = sucesso.</summary>
    public string? FailWith { get; set; }

    public Task<Result> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(FailWith is null
            ? Result.Success()
            : Result.Failure(Error.Failure(FailWith, "Falha simulada do dublê de INotificationsApi.")));
    }
}

/// <summary>Dublê de <see cref="ISignInService"/>: guarda cada entrada e devolve um usuário novo.</summary>
internal sealed class RecordingSignInService : ISignInService
{
    private readonly List<(string Email, SignInMethod Method)> _signIns = [];

    public IReadOnlyList<(string Email, SignInMethod Method)> SignIns => _signIns;

    public Task<Result<SignInResult>> SignInAsync(string email, SignInMethod method, CancellationToken cancellationToken)
    {
        _signIns.Add((email, method));
        return Task.FromResult(Result.Success(new SignInResult(Guid.CreateVersion7(), _signIns.Count == 1)));
    }
}

/// <summary>
/// SPEC-0016 — o token do link mágico: 256 bits aleatórios (base64url de 32 bytes ou hexadecimal de 64 caracteres); no
/// banco, só o hash SHA-256 dele (do texto ou dos bytes; em hex, base64 ou base64url).
/// </summary>
internal static class MagicLinkTokens
{
    public static void ShouldBe256Bits(string token)
    {
        var bytes = token.Length == 64 && token.All(Uri.IsHexDigit)
            ? Convert.FromHexString(token)
            : Base64Url.IsValid(token) ? Base64Url.DecodeFromChars(token) : [];
        bytes.Length.ShouldBe(32, $"o token \"{token}\" não tem 256 bits (base64url de 32 bytes ou hex de 64 caracteres)");
    }

    public static void ShouldBeHashOf(string stored, string token)
    {
        stored.ShouldNotContain(token, Case.Sensitive, "o token do link foi gravado em claro");
        var normalized = stored.StartsWith("\\x", StringComparison.Ordinal) ? stored[2..] : stored;
        Candidates(token).ShouldContain(
            candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.TrimEnd('='), normalized.TrimEnd('='), StringComparison.Ordinal),
            $"o valor gravado \"{stored}\" não é o SHA-256 do token");
    }

    private static IEnumerable<string> Candidates(string token)
    {
        var hashes = new List<byte[]> { SHA256.HashData(Encoding.UTF8.GetBytes(token)) };
        if (token.Length == 64 && token.All(Uri.IsHexDigit))
        {
            hashes.Add(SHA256.HashData(Convert.FromHexString(token)));
        }
        else if (Base64Url.IsValid(token))
        {
            hashes.Add(SHA256.HashData(Base64Url.DecodeFromChars(token)));
        }

        foreach (var hash in hashes)
        {
            yield return Convert.ToHexString(hash);
            yield return Convert.ToBase64String(hash);
            yield return Base64Url.EncodeToString(hash);
        }
    }
}
