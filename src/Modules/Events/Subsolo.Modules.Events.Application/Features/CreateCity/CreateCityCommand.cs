using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.CreateCity;

/// <summary>
/// SPEC-0017 — <c>POST /api/v1/admin/cities</c>: nova cidade com nome (2–60), UF (uma das 27), fuso IANA e slug
/// opcional (<see langword="null"/> → gerado do nome; fora dos endereços reservados). <paramref name="AdminId"/>: quem
/// cria, para o log de auditoria.
/// </summary>
public sealed record CreateCityCommand(string Name, string State, string TimeZone, string? Slug, Guid? AdminId = null)
    : ICommand<AdminCityResponse>;

/// <summary>SPEC-0017 — cidade no painel do admin: slug, nome, UF e fuso.</summary>
public sealed record AdminCityResponse(string Slug, string Name, string State, string TimeZone);
