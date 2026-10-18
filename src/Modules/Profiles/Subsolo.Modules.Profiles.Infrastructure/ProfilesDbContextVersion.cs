namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0021 (Emenda 3) — a propriedade de sombra do token de concorrência otimista de perfis e pedidos: um <c>uint</c>
/// com <c>IsRowVersion</c>, que o Npgsql liga à coluna de sistema <c>xmin</c> (nenhuma coluna nova).
/// </summary>
internal static class ProfilesDbContextVersion
{
    public const string Name = "Version";
}
