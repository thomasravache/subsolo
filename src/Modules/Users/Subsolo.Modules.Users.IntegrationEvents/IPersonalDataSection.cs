namespace Subsolo.Modules.Users.IntegrationEvents;

/// <summary>
/// SPEC-0019 — ponto de extensão do export de dados pessoais (<c>GET /api/v1/me/export</c>, LGPD art. 18): cada módulo
/// que guardar dado do usuário registra a sua seção no contêiner, e o export a inclui em <c>sections</c>.
/// </summary>
public interface IPersonalDataSection
{
    /// <summary>Chave da seção em <c>sections</c> (ex.: <c>follows</c>).</summary>
    string Name { get; }

    /// <summary>O que o módulo guarda de <paramref name="userId"/>; <see langword="null"/> = seção omitida.</summary>
    Task<object?> ExportAsync(Guid userId, CancellationToken ct);
}
