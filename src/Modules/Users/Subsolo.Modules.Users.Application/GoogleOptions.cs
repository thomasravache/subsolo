namespace Subsolo.Modules.Users.Application;

/// <summary>
/// SPEC-0020 — configuração do login com Google (seção <c>Users:Google</c>): o provedor OpenID Connect
/// (<see cref="Authority"/>; local e CI, o provedor falso; produção, o Google), o cliente (<see cref="ClientId"/>) e o
/// segredo do cliente (<see cref="ClientSecret"/>, só por segredo, ADR-0008).
/// </summary>
public sealed class GoogleOptions
{
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>Ligado só com <see cref="Authority"/> e <see cref="ClientId"/> preenchidos (<c>providers.google</c>).</summary>
    public bool IsEnabled => throw new NotImplementedException("SPEC-0020: implementar GoogleOptions.IsEnabled.");
}
