using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Application.Features.UpdateShow;

/// <summary>SPEC-0023 — a edição segue as regras do cadastro (<see cref="ShowFieldsValidator{T}"/>).</summary>
internal sealed class UpdateShowCommandValidator(ICityRepository cities, IStyleRepository styles, TimeProvider clock)
    : ShowFieldsValidator<UpdateShowCommand>(cities, styles, clock);
