using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.CreateCity;

namespace Subsolo.Modules.Events.Application.Features.UpdateCity;

/// <summary>
/// SPEC-0017 — <c>PUT /api/v1/admin/cities/{slug}</c>: novo nome, UF e fuso da cidade <paramref name="Slug"/> (o slug
/// não muda). <paramref name="AdminId"/>: quem edita, para o log de auditoria.
/// </summary>
public sealed record UpdateCityCommand(string Slug, string Name, string State, string TimeZone, Guid? AdminId = null)
    : ICommand<AdminCityResponse>;
