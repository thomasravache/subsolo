using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.ListStyles;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Application.Features.CreateStyle;

/// <summary>
/// SPEC-0017 — cria o estilo com o slug gerado do nome; slug já existente → <c>events.style_taken</c> (Conflict).
/// </summary>
#pragma warning disable CS9113 // SPEC-0017: andaime do contrato; os parâmetros são usados na implementação.
internal sealed class CreateStyleCommandHandler(
    IStyleRepository styles,
    IEventsUnitOfWork unitOfWork,
    ILogger<CreateStyleCommandHandler> logger)
    : ICommandHandler<CreateStyleCommand, StyleResponse>
#pragma warning restore CS9113
{
    public Task<Result<StyleResponse>> Handle(CreateStyleCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0017: CreateStyle ainda não implementado.");
}
