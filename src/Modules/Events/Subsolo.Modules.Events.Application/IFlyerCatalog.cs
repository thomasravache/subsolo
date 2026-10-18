namespace Subsolo.Modules.Events.Application;

/// <summary>
/// SPEC-0022 — catálogo interno dos flyers enviados: o cadastro de show confere que o flyer existe e o liga ao show
/// (flyer não ligado há mais de 24 h é apagado pela limpeza).
/// </summary>
public interface IFlyerCatalog
{
    Task<bool> ExistsAsync(string flyerId, CancellationToken ct);

    Task AttachAsync(string flyerId, CancellationToken ct);
}
