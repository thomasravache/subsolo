namespace Subsolo.Modules.Moderation.Domain.Reports;

/// <summary>SPEC-0033 — os motivos da denúncia (§6): <c>fake | cancelled_unannounced | wrong_info | other</c>.</summary>
public static class ReportReasons
{
    public const string Fake = "fake";

    public const string CancelledUnannounced = "cancelled_unannounced";

    public const string WrongInfo = "wrong_info";

    public const string Other = "other";

    /// <summary>Os quatro motivos, na ordem da folha "Reportar show".</summary>
    public static readonly IReadOnlyList<string> All = [Fake, CancelledUnannounced, WrongInfo, Other];

    /// <summary>Tamanho máximo do detalhe (§6: <c>details</c> com 0–500 caracteres).</summary>
    public const int DetailsMaxLength = 500;
}
