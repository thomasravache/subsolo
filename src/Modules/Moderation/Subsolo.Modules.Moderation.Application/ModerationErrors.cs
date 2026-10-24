namespace Subsolo.Modules.Moderation.Application;

/// <summary>SPEC-0033 — códigos de erro do módulo Moderation (vão em <c>extensions.code</c> do ProblemDetails).</summary>
public static class ModerationErrors
{
    /// <summary>400: denúncia inválida (motivo desconhecido, <c>other</c> sem detalhe, detalhe com mais de 500).</summary>
    public const string InvalidReport = "moderation.invalid_report";

    /// <summary>409: o fã já tem denúncia pendente deste show.</summary>
    public const string AlreadyReported = "moderation.already_reported";

    /// <summary>404: o show não tem denúncias pendentes (já decidido ou nunca denunciado).</summary>
    public const string NoPendingReports = "moderation.no_pending_reports";
}
