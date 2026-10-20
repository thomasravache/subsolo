using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Application.Features.ListSupporters;

/// <summary>SPEC-0028 — os apoiadores do repositório (ordem de cadastro) como <c>{ id, name, url }</c>.</summary>
internal sealed class ListSupportersQueryHandler(ISupporterRepository supporters)
    : IQueryHandler<ListSupportersQuery, IReadOnlyList<SupporterResponse>>
{
    public async Task<Result<IReadOnlyList<SupporterResponse>>> Handle(ListSupportersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var all = await supporters.ListAsync(cancellationToken);
        IReadOnlyList<SupporterResponse> response =
            [.. all.Select(supporter => new SupporterResponse(supporter.Id, supporter.Name, supporter.Url))];
        return Result.Success(response);
    }
}
