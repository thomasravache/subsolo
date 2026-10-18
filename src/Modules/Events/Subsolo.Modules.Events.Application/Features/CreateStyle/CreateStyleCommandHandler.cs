using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.ListStyles;
using Subsolo.Modules.Events.Domain.Slugs;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Application.Features.CreateStyle;

/// <summary>
/// SPEC-0017 — cria o estilo com o slug gerado do nome; slug já existente → <c>events.style_taken</c> (Conflict); nome
/// que não gera slug válido → <c>events.invalid_style</c>. Registra no log de auditoria quem criou.
/// </summary>
internal sealed partial class CreateStyleCommandHandler(
    IStyleRepository styles,
    IEventsUnitOfWork unitOfWork,
    ILogger<CreateStyleCommandHandler> logger)
    : ICommandHandler<CreateStyleCommand, StyleResponse>
{
    public async Task<Result<StyleResponse>> Handle(CreateStyleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var name = command.Name.Trim();
        var slug = SlugGenerator.FromName(name);
        if (!Regex.IsMatch(slug, SlugGenerator.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return Result.Failure<StyleResponse>(EventsErrors.FieldError(
                EventsErrors.InvalidStyle, "Nome do estilo inválido.", nameof(command.Name), EventsErrors.GeneratedSlugMessage));
        }

        if (await styles.GetBySlugAsync(slug, cancellationToken) is not null)
        {
            return Result.Failure<StyleResponse>(EventsErrors.StyleTakenError());
        }

        var style = Style.Create(slug, name);
        styles.Add(style);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogStyleCreated(logger, command.AdminId, style.Slug);
        return Result.Success(new StyleResponse(style.Slug, style.Name));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: admin {AdminId} criou o estilo {StyleSlug}")]
    private static partial void LogStyleCreated(ILogger logger, Guid? adminId, string styleSlug);
}
