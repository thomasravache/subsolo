using Microsoft.AspNetCore.Builder;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0014 — marca um endpoint com dados de quem está logado: toda resposta dele (inclusive 401, 400 do
/// anti-forgery e 429) sai com <c>Cache-Control: no-store</c>, aplicado pelo middleware de segurança do host.
/// </summary>
public sealed class NoStoreMetadata
{
    /// <summary>Valor do cabeçalho <c>Cache-Control</c>.</summary>
    public const string CacheControl = "no-store";
}

/// <summary>SPEC-0014 — <c>.RequireNoStore()</c> nos endpoints com dados do usuário.</summary>
public static class NoStoreExtensions
{
    public static TBuilder RequireNoStore<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new NoStoreMetadata());
}
