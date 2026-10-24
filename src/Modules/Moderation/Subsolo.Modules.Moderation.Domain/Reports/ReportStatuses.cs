namespace Subsolo.Modules.Moderation.Domain.Reports;

/// <summary>SPEC-0033 — a situação da denúncia (§6): <c>pending | kept | hidden</c>.</summary>
public static class ReportStatuses
{
    /// <summary>Esperando a decisão do admin (aparece na fila).</summary>
    public const string Pending = "pending";

    /// <summary>O admin manteve o show (denúncia arquivada).</summary>
    public const string Kept = "kept";

    /// <summary>O admin ocultou o show.</summary>
    public const string Hidden = "hidden";
}
