using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Application.Features.ListStyles;

/// <summary>SPEC-0017 — os estilos do repositório (ordenados pelo nome) como <c>{ slug, name }</c>.</summary>
internal sealed class ListStylesQueryHandler(IStyleRepository styles)
    : IQueryHandler<ListStylesQuery, IReadOnlyList<StyleResponse>>
{
    public async Task<Result<IReadOnlyList<StyleResponse>>> Handle(ListStylesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var all = await styles.ListAsync(cancellationToken);
        IReadOnlyList<StyleResponse> response = [.. all.Select(style => new StyleResponse(style.Slug, style.Name))];
        return Result.Success(response);
    }
}
