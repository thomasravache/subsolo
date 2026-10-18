using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Slugs;

namespace Subsolo.Modules.Profiles.Application.Features.SearchClaimableProfiles;

/// <summary>
/// SPEC-0021 — busca pelo nome (sem diferenciar caixa) ou pelo slug (sem acento) entre os perfis aprovados sem dono;
/// termo com menos de <see cref="MinTermLength"/> caracteres ou tipo desconhecido → lista vazia.
/// </summary>
internal sealed class SearchClaimableProfilesQueryHandler(IProfilesReadModel profiles)
    : IQueryHandler<SearchClaimableProfilesQuery, IReadOnlyList<ClaimableProfileResponse>>
{
    /// <summary>Tamanho mínimo do termo.</summary>
    public const int MinTermLength = 2;

    /// <summary>Máximo de resultados.</summary>
    public const int Limit = 20;

    public async Task<Result<IReadOnlyList<ClaimableProfileResponse>>> Handle(
        SearchClaimableProfilesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = query.Q?.Trim() ?? string.Empty;
        var type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim();
        if (term.Length < MinTermLength || (type is not null && !ProfileTypes.All.Contains(type, StringComparer.Ordinal)))
        {
            return Result.Success<IReadOnlyList<ClaimableProfileResponse>>([]);
        }

        var found = await profiles.SearchClaimableAsync(term, ProfileSlug.FromName(term), type, Limit, cancellationToken);
        return Result.Success(found);
    }
}
