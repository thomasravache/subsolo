using System.Globalization;
using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.DailyDigest;
using Subsolo.Modules.Engagement.Application.Features.Shared;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// Itens herdados da revisão da SPEC-0031 sobre os limites de texto dos avisos:
/// <list type="bullet">
/// <item>SPEC-0032 H31-06 — <see cref="DigestBuilder"/>: com 20 shows de line-up longo (cortado em 200 caracteres), o
/// corpo do resumo cabe em <see cref="DigestBuilder.MaxBodyLength"/> (1.000): entram as primeiras linhas (pela data) que
/// cabem, e as que não cabem vão para "e mais {n} na agenda"; a próxima linha não caberia.</item>
/// <item>SPEC-0032 H31-08 — <see cref="NoticeText.Lineup"/>: o corte do line-up longo cai num limite de elemento de texto
/// (<see cref="StringInfo"/>), sem partir um par substituto (emoji no nome da banda) nem uma bandeira; o resultado termina
/// em "…" e não passa de <see cref="NoticeText.MaxLineupLength"/> caracteres.</item>
/// </list>
/// </summary>
public sealed class NoticeTextLimitsTests
{
    private const int Shows = 20;

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-06")]
    public async Task LinesThatDoNotFitInOneThousandCharactersGoToAndMore()
    {
        var harness = new EngagementHarness();
        var start = EngagementHarness.Start;
        var published = new List<PublishedShow>();
        var expectedLines = new List<string>();
        for (var day = 1; day <= Shows; day++)
        {
            var id = Guid.CreateVersion7();
            IReadOnlyList<string> lineup =
            [
                .. Enumerable.Range(1, 10).Select(band => string.Create(CultureInfo.InvariantCulture, $"Banda Muito Comprida {day:00}-{band:00}")),
            ];
            var startsAt = new DateTimeOffset(start.Date.AddDays(day).AddHours(23), TimeSpan.Zero);
            harness.Catalog.AddSummary(new ShowSummary(
                id, $"show-{id:N}", "sao-paulo", "São Paulo", "America/Sao_Paulo", lineup, "Hangar", startsAt, "scheduled"));
            var row = new PublishedShow(id, "sao-paulo", [], start.AddHours(-2));
            await harness.PublishedShows.AddAsync(row, TestContext.Current.CancellationToken);
            published.Add(row);
            expectedLines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{start.Date.AddDays(day):dd/MM} · {NoticeText.Lineup(lineup)} · Hangar, São Paulo"));
        }

        var fan = Guid.CreateVersion7();
        harness.AddFollow(fan, FollowTarget.City("sao-paulo"));

        var notices = await harness.Create<DigestBuilder>().BuildAsync(published, TestContext.Current.CancellationToken);

        var body = notices.ShouldHaveSingleItem().Notice.Body;
        body.Length.ShouldBeLessThanOrEqualTo(DigestBuilder.MaxBodyLength, $"corpo com {body.Length} caracteres");
        var lines = body.Split('\n');
        var listed = lines.Length - 1;
        listed.ShouldBeGreaterThan(0, "nenhuma linha de show no corpo");
        listed.ShouldBeLessThan(Shows, "os 20 shows de line-up longo não cabem em 1.000 caracteres");
        lines[..listed].ShouldBe(expectedLines.Take(listed), "as linhas listadas são as primeiras pela data, inteiras");
        lines[^1].ShouldBe(string.Create(CultureInfo.InvariantCulture, $"e mais {Shows - listed} na agenda"));
        var withNext = string.Join(
            '\n',
            lines[..listed].Append(expectedLines[listed]).Append(string.Create(CultureInfo.InvariantCulture, $"e mais {Shows - listed - 1} na agenda")));
        withNext.Length.ShouldBeGreaterThan(DigestBuilder.MaxBodyLength, "cabia mais uma linha e ela foi para o \"e mais\"");
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-08")]
    public void AnEmojiAtTheCutIsNotSplit()
    {
        // O caractere 199 (índice 198) é a primeira metade de um emoji: o corte em 199 caracteres partiria o par.
        var name = new string('A', NoticeText.MaxLineupLength - 2) + string.Concat(Enumerable.Repeat("🤘", 10));

        var lineup = NoticeText.Lineup([name]);

        ShouldBeCutAtATextElementBoundary(name, lineup);
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-08")]
    public void AFlagAtTheCutIsNotSplit()
    {
        // Bandeira (dois indicadores regionais, quatro unidades UTF-16) atravessando o ponto de corte.
        var name = new string('B', NoticeText.MaxLineupLength - 15) + string.Concat(Enumerable.Repeat("🇧🇷", 5));

        var lineup = NoticeText.Lineup(["Cova Rasa", name]);

        ShouldBeCutAtATextElementBoundary($"Cova Rasa + {name}", lineup);
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-08")]
    public void AShortLineupWithEmojiIsKeptWhole()
    {
        NoticeText.Lineup(["Cova Rasa 🤘", "Peste Negra 🇧🇷"]).ShouldBe("Cova Rasa 🤘 + Peste Negra 🇧🇷");
    }

    private static void ShouldBeCutAtATextElementBoundary(string full, string cut)
    {
        cut.ShouldEndWith("…");
        cut.Length.ShouldBeLessThanOrEqualTo(NoticeText.MaxLineupLength, $"line-up com {cut.Length} caracteres");
        var kept = cut[..^1];
        full.ShouldStartWith(kept, Case.Sensitive, "o line-up cortado não é o começo do original");
        StringInfo.ParseCombiningCharacters(full).Append(full.Length).ShouldContain(
            kept.Length, $"o corte em {kept.Length} caracteres parte um elemento de texto");
        for (var index = 0; index < cut.Length; index++)
        {
            if (char.IsHighSurrogate(cut[index]))
            {
                (index + 1 < cut.Length && char.IsLowSurrogate(cut[index + 1])).ShouldBeTrue($"metade de par substituto em {index}");
                index++;
            }
            else
            {
                char.IsLowSurrogate(cut[index]).ShouldBeFalse($"metade de par substituto em {index}");
            }
        }
    }
}
