using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.CreateCity;

namespace Subsolo.Modules.Events.Application.Features.ListCitiesForAdmin;

/// <summary>SPEC-0017 — <c>GET /api/v1/admin/cities</c>: todas as cidades, com UF e fuso, ordenadas pelo nome.</summary>
public sealed record ListCitiesForAdminQuery : IQuery<IReadOnlyList<AdminCityResponse>>;
