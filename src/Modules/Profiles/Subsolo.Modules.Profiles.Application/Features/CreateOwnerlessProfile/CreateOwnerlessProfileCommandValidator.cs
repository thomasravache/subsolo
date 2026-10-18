using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Application.Features.Shared;

namespace Subsolo.Modules.Profiles.Application.Features.CreateOwnerlessProfile;

/// <summary>SPEC-0021 — as regras do pedido (<see cref="ProfileFieldsValidator{T}"/>), sem o contato.</summary>
internal sealed class CreateOwnerlessProfileCommandValidator(IEventsCatalog catalog)
    : ProfileFieldsValidator<CreateOwnerlessProfileCommand>(catalog);
