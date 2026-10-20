using Subsolo.Modules.Engagement.Domain.Attendances;

namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0029 — leituras dos "vou" do fã para a lista dele e o export.</summary>
public interface IAttendancesReadModel
{
    /// <summary>Os "vou" de <paramref name="userId"/>, do mais antigo para o mais novo.</summary>
    Task<IReadOnlyList<Attendance>> ListOfAsync(Guid userId, CancellationToken cancellationToken);
}
