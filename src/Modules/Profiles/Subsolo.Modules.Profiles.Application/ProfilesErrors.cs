using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Application;

/// <summary>SPEC-0021 — códigos de erro do módulo Profiles (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class ProfilesErrors
{
    /// <summary>400: dados do pedido (ou do perfil criado pelo admin) inválidos; os campos vão em <c>errors</c>.</summary>
    public const string InvalidRequest = "profiles.invalid_request";

    /// <summary>409: o usuário já tem um pedido em análise.</summary>
    public const string RequestPending = "profiles.request_pending";

    /// <summary>409: já existe perfil do mesmo tipo com esse nome (slug).</summary>
    public const string NameTaken = "profiles.name_taken";

    /// <summary>404: perfil inexistente (ou não reivindicável).</summary>
    public const string NotFound = "profiles.not_found";

    /// <summary>409: o perfil já tem dono.</summary>
    public const string AlreadyOwned = "profiles.already_owned";

    /// <summary>404: pedido inexistente.</summary>
    public const string RequestNotFound = "profiles.request_not_found";

    /// <summary>409: o pedido já foi decidido.</summary>
    public const string RequestDecided = "profiles.request_decided";

    /// <summary>SPEC-0026 — 400: busca de perfis inválida (<c>q</c> com menos de 2 caracteres ou tipo desconhecido).</summary>
    public const string InvalidQuery = "profiles.invalid_query";

    internal static Error RequestPendingError() =>
        Error.Conflict(RequestPending, "Você já tem um pedido em análise; espere a decisão do admin.");

    internal static Error NameTakenError() =>
        Error.Conflict(NameTaken, "Já existe um perfil desse tipo com esse nome.");

    internal static Error NotFoundError() => Error.NotFound(NotFound, "Perfil não encontrado.");

    internal static Error AlreadyOwnedError() => Error.Conflict(AlreadyOwned, "Esse perfil já tem dono.");

    internal static Error RequestNotFoundError() => Error.NotFound(RequestNotFound, "Pedido não encontrado.");

    internal static Error InvalidQueryError(string field, string message) =>
        Error.Validation(
            InvalidQuery,
            "Busca de perfis inválida.",
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    internal static Error RequestDecidedError() => Error.Conflict(RequestDecided, "Esse pedido já foi decidido.");
}
