using System.Globalization;
using System.Text;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Engagement.IntegrationEvents;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.DailyDigest;

/// <summary>
/// SPEC-0031 — o montador do resumo diário (§6): dos shows publicados nas últimas 24 h ainda <c>scheduled</c> e futuros,
/// um aviso <c>digest</c> por seguidor com os shows dele, sem repetição (até <see cref="MaxShows"/>, pela data, uma linha
/// "{dd/MM} · {Lineup} · {VenueName}, {CityName}" por show e "e mais {n} na agenda"); quem não tem show novo fica de fora.
/// Os seguidores de cada show vêm do <see cref="IEngagementApi.GetFollowersAsync"/> (cidade e perfis do show); nada do
/// resumo é guardado por usuário (ADR-0008: recalculado a partir dos follows).
/// </summary>
internal sealed class DigestBuilder(IEventsCatalog catalog, IEngagementApi engagement, TimeProvider clock)
{
    /// <summary>Máximo de shows listados no corpo do resumo.</summary>
    public const int MaxShows = 20;

    public const string Kind = "digest";

    public const string Title = "Shows novos de quem você segue";

    /// <summary>A agenda do site (o corpo não tem link por show).</summary>
    public const string Url = "/";

    /// <summary>Tamanho máximo do corpo de um aviso no Notifications (SPEC-0030): as linhas que não cabem entram no "e mais".</summary>
    public const int MaxBodyLength = 1000;

    public async Task<IReadOnlyList<DigestNotice>> BuildAsync(
        IReadOnlyCollection<PublishedShow> published, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(published);
        var now = clock.GetUtcNow();
        var showsOf = new Dictionary<Guid, List<ShowSummary>>();
        foreach (var row in published.DistinctBy(row => row.ShowId))
        {
            var show = await catalog.GetShowSummaryAsync(row.ShowId, cancellationToken);
            if (show is null || !NoticeText.IsUpcoming(show, now))
            {
                continue;
            }

            foreach (var follower in await engagement.GetFollowersAsync(row.CitySlug, row.ProfileIds, cancellationToken))
            {
                if (!showsOf.TryGetValue(follower, out var shows))
                {
                    showsOf[follower] = shows = [];
                }

                shows.Add(show);
            }
        }

        if (showsOf.Count == 0)
        {
            return [];
        }

        var key = $"digest:{NoticeText.Local(now, NoticeText.SaoPaulo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var lines = new Dictionary<Guid, string>();
        return
        [
            .. showsOf.Select(pair => new DigestNotice(
                pair.Key,
                new Notice(Kind, key, Title, Body(pair.Value, lines), Url))),
        ];
    }

    /// <summary>As linhas dos shows (pela data), até <see cref="MaxShows"/> e o que couber no corpo, e "e mais {n} na agenda".</summary>
    private static string Body(List<ShowSummary> shows, Dictionary<Guid, string> lines)
    {
        var ordered = shows.OrderBy(show => show.StartsAtUtc).ThenBy(show => show.ShowId).ToList();
        var body = new StringBuilder();
        var listed = 0;
        foreach (var show in ordered.Take(MaxShows))
        {
            if (!lines.TryGetValue(show.ShowId, out var line))
            {
                lines[show.ShowId] = line = Line(show);
            }

            var rest = ordered.Count - listed - 1;
            var length = body.Length + (listed > 0 ? 1 : 0) + line.Length + (rest > 0 ? More(rest).Length + 1 : 0);
            if (listed > 0 && length > MaxBodyLength)
            {
                break;
            }

            if (listed > 0)
            {
                body.Append('\n');
            }

            body.Append(line);
            listed++;
        }

        if (ordered.Count > listed)
        {
            body.Append('\n').Append(More(ordered.Count - listed));
        }

        return body.ToString();
    }

    /// <summary>"{dd/MM} · {Lineup} · {VenueName}, {CityName}" (a data no fuso da cidade).</summary>
    private static string Line(ShowSummary show) =>
        $"{NoticeText.Day(NoticeText.Local(show.StartsAtUtc, show.CityTimeZone))} · {NoticeText.Lineup(show.Lineup)} · {show.VenueName}, {show.CityName}";

    private static string More(int count) => string.Create(CultureInfo.InvariantCulture, $"e mais {count} na agenda");
}

/// <summary>SPEC-0031 — o aviso <c>digest</c> de um fã.</summary>
internal sealed record DigestNotice(Guid UserId, Notice Notice);
