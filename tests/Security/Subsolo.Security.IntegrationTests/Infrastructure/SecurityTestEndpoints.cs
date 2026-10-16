using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.Security.IntegrationTests.Infrastructure;

/// <summary>
/// Dublês: endpoints de exemplo que existem só neste projeto de teste (registrados pelo
/// <see cref="SecurityApiFactory"/>), cada um com a declaração de segurança que o teste exercita.
/// </summary>
public sealed class SecurityTestEndpoints : IEndpoint
{
    /// <summary>Sem declaração: cai na política de fallback (usuário autenticado).</summary>
    public const string Fallback = "/api/v1/__tests/security/fallback";

    public const string Fan = "/api/v1/__tests/security/fan";

    public const string Artist = "/api/v1/__tests/security/artist";

    public const string Admin = "/api/v1/__tests/security/admin";

    /// <summary>Mutação pública (AllowAnonymous): só o anti-forgery decide.</summary>
    public const string Mutation = "/api/v1/__tests/security/mutation";

    /// <summary>Prefixo dos endpoints públicos com rate limiting; o sufixo é o nome da política.</summary>
    public const string RateLimitedPrefix = "/api/v1/__tests/security/rate-limited/";

    public static readonly string[] RateLimitPolicies = ["auth", "report", "default"];

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(Fallback, () => Results.Ok());
        app.MapGet(Fan, () => Results.Ok()).RequireAuthorization("fan");
        app.MapGet(Artist, () => Results.Ok()).RequireAuthorization("artist");
        app.MapGet(Admin, () => Results.Ok()).RequireAuthorization("admin");
        app.MapPost(Mutation, () => Results.Ok()).AllowAnonymous();
        foreach (var policy in RateLimitPolicies)
        {
            app.MapGet(RateLimitedPrefix + policy, () => Results.Ok()).AllowAnonymous().RequireRateLimiting(policy);
        }
    }
}
