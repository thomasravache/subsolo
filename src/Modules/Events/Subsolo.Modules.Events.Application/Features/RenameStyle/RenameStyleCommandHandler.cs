using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.ListStyles;
using Subsolo.Modules.Events.Domain.Slugs;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Application.Features.RenameStyle;

/// <summary>
/// SPEC-0017 — troca o nome do estilo (o slug não muda); estilo inexistente → <c>events.style_not_found</c>
/// (NotFound); nome igual ao de outro estilo (ou que gera o slug de outro) → <c>events.style_taken</c> (Conflict).
/// Registra no log de auditoria quem renomeou.
/// </summary>
internal sealed partial class RenameStyleCommandHandler(
    IStyleRepository styles,
    IEventsUnitOfWork unitOfWork,
    ILogger<RenameStyleCommandHandler> logger)
    : ICommandHandler<RenameStyleCommand, StyleResponse>
{
    public async Task<Result<StyleResponse>> Handle(RenameStyleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var style = await styles.GetBySlugAsync(command.Slug, cancellationToken);
        if (style is null)
        {
            return Result.Failure<StyleResponse>(EventsErrors.StyleNotFoundError(command.Slug));
        }

        var name = command.Name.Trim();
        var slug = SlugGenerator.FromName(name);
        var others = await styles.ListAsync(cancellationToken);
        if (others.Any(other => other.Slug != style.Slug
                && (other.Slug == slug || string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase))))
        {
            return Result.Failure<StyleResponse>(EventsErrors.StyleTakenError());
        }

        style.Rename(name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogStyleRenamed(logger, command.AdminId, style.Slug);
        return Result.Success(new StyleResponse(style.Slug, style.Name));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: admin {AdminId} renomeou o estilo {StyleSlug}")]
    private static partial void LogStyleRenamed(ILogger logger, Guid? adminId, string styleSlug);
}
