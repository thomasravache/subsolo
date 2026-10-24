using Subsolo.Modules.Moderation.Application.Features.ListReports;

namespace Subsolo.Modules.Moderation.Application;

/// <summary>SPEC-0033 — leituras e gravações em lote de <c>moderation.reports</c> (sem passar pelo domínio).</summary>
internal interface IReportsReadModel
{
    /// <summary>Todas as denúncias pendentes, sem quem denunciou (só show, motivo, detalhe e data).</summary>
    Task<IReadOnlyList<PendingReport>> ListPendingAsync(CancellationToken cancellationToken);

    /// <summary>A conta foi excluída: as denúncias de <paramref name="userId"/> ficam sem autor; devolve quantas.</summary>
    Task<int> ForgetReporterAsync(Guid userId, CancellationToken cancellationToken);
}
