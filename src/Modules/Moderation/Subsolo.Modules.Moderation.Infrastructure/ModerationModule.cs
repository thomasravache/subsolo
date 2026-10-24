using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Modules.Moderation.Infrastructure;

/// <summary>
/// SPEC-0033 — scaffold do registro do módulo Moderation (<c>services.AddModerationModule(configuration)</c> no host).
/// Implementação pendente: ainda não é chamado pelo <c>Program.cs</c>.
/// </summary>
public static class ModerationModule
{
    /// <summary>Nome do <c>Meter</c> do módulo (<c>subsolo.moderation.reports</c>, <c>subsolo.moderation.decisions</c>).</summary>
    public const string MeterName = "Subsolo.Moderation";

    public static IServiceCollection AddModerationModule(this IServiceCollection services, IConfiguration configuration) =>
        throw new NotImplementedException("SPEC-0033: registro do módulo Moderation");
}
