namespace Subsolo.Modules.Profiles.Application;

/// <summary>SPEC-0021 (Emenda 3) — o que a gravação concorrente esbarrou.</summary>
public enum ProfilesConflict
{
    /// <summary>O pedido mudou entre a leitura e a gravação (outra decisão chegou antes; token otimista).</summary>
    RequestChanged,

    /// <summary>O perfil mudou entre a leitura e a gravação (outra reivindicação lhe deu dono antes; token otimista).</summary>
    ProfileChanged,

    /// <summary>Já existe perfil do mesmo tipo com o slug (índice único).</summary>
    NameTaken,

    /// <summary>O usuário já tem um pedido em análise (índice único parcial dos pendentes).</summary>
    RequestPending,
}

/// <summary>
/// SPEC-0021 (Emenda 3) — a gravação perdeu uma corrida: token de concorrência (<c>xmin</c>) ou violação de unicidade
/// (23505). Lançada pelo <see cref="IProfilesUnitOfWork"/> depois de descartar as mudanças; os handlers a convertem no
/// 409 do contrato, nunca 500.
/// </summary>
public sealed class ProfilesConflictException : Exception
{
    public ProfilesConflictException()
    {
    }

    public ProfilesConflictException(string message)
        : base(message)
    {
    }

    public ProfilesConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ProfilesConflictException(ProfilesConflict conflict, Exception innerException)
        : base($"Gravação concorrente: {conflict}.", innerException) =>
        Conflict = conflict;

    public ProfilesConflict Conflict { get; }
}
