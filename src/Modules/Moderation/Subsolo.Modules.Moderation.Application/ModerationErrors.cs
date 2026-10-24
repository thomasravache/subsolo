using Subsolo.Common.Domain;
using Subsolo.Modules.Moderation.Domain.Reports;

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

    /// <summary>400: a nota interna de quem oculta passa de 200 caracteres.</summary>
    public const string InvalidNote = "moderation.invalid_note";

    /// <summary>
    /// 404: o show denunciado não existe (ou foi ocultado). O código é o do Events (§6), que o Moderation repete sem
    /// depender da aplicação do Events (ADR-0001).
    /// </summary>
    public const string ShowNotFound = "events.show_not_found";

    internal static Error ShowNotFoundError() => Error.NotFound(ShowNotFound, "Show não encontrado.");

    internal static Error AlreadyReportedError() => Error.Conflict(AlreadyReported, "Você já denunciou este show.");

    internal static Error NoPendingReportsError() =>
        Error.NotFound(NoPendingReports, "Este show não tem denúncias pendentes.");

    /// <summary>Tamanho máximo da nota (o mesmo da coluna <c>decision_note</c>).</summary>
    internal const int NoteMaxLength = Report.DecisionNoteMaxLength;
}
