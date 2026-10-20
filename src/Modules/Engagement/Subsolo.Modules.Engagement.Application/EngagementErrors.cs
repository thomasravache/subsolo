using Subsolo.Common.Domain;

namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0027 — códigos de erro do módulo Engagement (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class EngagementErrors
{
    /// <summary>404: cidade inexistente, ou perfil inexistente ou não aprovado.</summary>
    public const string TargetNotFound = "engagement.target_not_found";

    internal static Error TargetNotFoundError() => Error.NotFound(TargetNotFound, "Não encontramos o que você quer seguir.");
}
