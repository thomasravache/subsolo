using Subsolo.Modules.Engagement.Application.Features.NotifyNewShow;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0031 UT-01 — o compositor do aviso <c>new_show</c> (§6): dado o <see cref="ShowSummary"/> de "Noite da Peste II"
/// em Curitiba com o line-up ["Cova Rasa", "Peste Negra"], a casa "Basement", início 2026-11-15T01:00Z e o fuso
/// America/Sao_Paulo, o aviso tem o título "Show novo em Curitiba", o corpo
/// "Cova Rasa + Peste Negra · sáb 14/11 22h00 · Basement" (dia e hora no fuso da cidade), a url
/// "/curitiba/noite-da-peste-ii" e a chave "new_show:{ShowId}".
/// </summary>
public sealed class NewShowNoticeTests
{
    private static readonly ShowSummary NoiteDaPeste = new(
        Guid.Parse("0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42"),
        "noite-da-peste-ii",
        "curitiba",
        "Curitiba",
        "America/Sao_Paulo",
        ["Cova Rasa", "Peste Negra"],
        "Basement",
        new DateTimeOffset(2026, 11, 15, 1, 0, 0, TimeSpan.Zero),
        "scheduled");

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-01")]
    public void TheNoticeHasTheTitleBodyAndUrlOfTheContract()
    {
        var notice = NewShowNotice.Compose(NoiteDaPeste);

        notice.ShouldSatisfyAllConditions(
            () => notice.Kind.ShouldBe("new_show"),
            () => notice.Title.ShouldBe("Show novo em Curitiba"),
            () => notice.Body.ShouldBe("Cova Rasa + Peste Negra · sáb 14/11 22h00 · Basement"),
            () => notice.Url.ShouldBe("/curitiba/noite-da-peste-ii"));
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-01")]
    public void TheDedupeKeyIsNewShowAndTheShowId()
    {
        var notice = NewShowNotice.Compose(NoiteDaPeste);

        notice.DedupeKey.ShouldBe("new_show:0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-01")]
    public void TheDayAndTimeAreInTheTimeZoneOfTheCity()
    {
        // 2026-11-15T03:30Z: 00h30 de domingo em São Paulo (UTC-3), 23h30 de sábado em Manaus (UTC-4).
        var manaus = NoiteDaPeste with
        {
            CitySlug = "manaus",
            CityName = "Manaus",
            CityTimeZone = "America/Manaus",
            Lineup = ["Peste Negra"],
            StartsAtUtc = new DateTimeOffset(2026, 11, 15, 3, 30, 0, TimeSpan.Zero),
        };

        var notice = NewShowNotice.Compose(manaus);

        notice.Title.ShouldBe("Show novo em Manaus");
        notice.Body.ShouldBe("Peste Negra · sáb 14/11 23h30 · Basement");
        notice.Url.ShouldBe("/manaus/noite-da-peste-ii");
    }
}
