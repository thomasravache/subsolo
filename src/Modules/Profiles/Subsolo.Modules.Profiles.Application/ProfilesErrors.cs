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
}
