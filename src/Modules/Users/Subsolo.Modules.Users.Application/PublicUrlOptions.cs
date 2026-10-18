namespace Subsolo.Modules.Users.Application;

/// <summary>
/// SPEC-0016 — endereço público do site (seção <see cref="SectionName"/>, chave <c>App:PublicUrl</c>), base do link
/// mágico do e-mail: <c>{App:PublicUrl}/entrar/confirmar?token=…</c>.
/// </summary>
public sealed class PublicUrlOptions
{
    public const string SectionName = "App";

    /// <summary><c>App:PublicUrl</c>: URL absoluta do site (ex.: <c>https://localhost:8443</c> na pilha local).</summary>
    public string PublicUrl { get; set; } = string.Empty;
}
