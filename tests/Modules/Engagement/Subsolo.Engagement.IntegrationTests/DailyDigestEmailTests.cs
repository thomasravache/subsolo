using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0031 IT-05 — com o Mailpit real e o relógio fixo: dois fãs logados ligam o resumo diário por e-mail; um segue a
/// cidade de desenvolvimento, o outro só a banda "Necrose"; há dois shows novos (registrados em <c>published_shows</c> há
/// 1 hora): um com "Cova Rasa" (sem perfil) e um com a Necrose. O job <c>Engagement.DailyDigest</c> roda duas vezes no
/// mesmo dia: cada fã recebe um e-mail só, "Shows novos de quem você segue", com os seus shows (quem segue a banda não vê
/// o outro), o link da agenda (<c>{App:PublicUrl}/</c>) e o link de descadastro; a segunda execução não manda outro.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed partial class DailyDigestEmailTests(PostgresFixture postgres, MailpitFixture mailpit) : IClassFixture<MailpitFixture>
{
    private const string Subject = "Shows novos de quem você segue";

    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task EachFanGetsOneDigestEmailWithTheirShowsEvenIfTheJobRunsTwice()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            clock,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Notifications:Smtp:Host"] = mailpit.SmtpHost,
                ["Notifications:Smtp:Port"] = mailpit.SmtpPort.ToString(CultureInfo.InvariantCulture),
                ["Notifications:Smtp:Security"] = "None",
                ["Notifications:Unsubscribe:SigningKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            });
        var cityFanEmail = $"fa.cidade-{Guid.NewGuid():N}@subsolo.test";
        var bandFanEmail = $"fa.banda-{Guid.NewGuid():N}@subsolo.test";
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (cityFan, _) = await api.SignedInAsync(cityFanEmail, ct);
        var (bandFan, _) = await api.SignedInAsync(bandFanEmail, ct);
        using var a = admin;
        using var c = cityFan;
        using var b = bandFan;
        var band = await admin.CreateApprovedProfileAsync("Necrose", ct);
        await cityFan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        await bandFan.PutAsync(EngagementRequests.FollowProfile(band), ct).ShouldSucceedAsync(ct);
        await cityFan.SetPreferencesAsync(newShowPush: false, dailyDigestEmail: true, ct);
        await bandFan.SetPreferencesAsync(newShowPush: false, dailyDigestEmail: true, ct);

        var now = clock.GetUtcNow();
        var withoutProfile = await admin.CreateShowAsync("Noite Sem Perfil", now, days: 3, ct);
        var withBand = await admin.CreateShowAsync("Noite da Necrose", now, days: 5, ct, band: band);
        await NoticesDatabase.SeedPublishedShowsAsync(
            api.ConnectionString,
            [
                new PublishedShowRow(withoutProfile.Id, EngagementApiFactory.City, [], now.AddHours(-1)),
                new PublishedShowRow(withBand.Id, EngagementApiFactory.City, [band], now.AddHours(-1)),
            ],
            ct);
        var coveRasaLine = $"{withoutProfile.Day} · Cova Rasa · {NoticeRequests.Venue}, {NoticeRequests.CityName}";
        var necroseLine = $"{withBand.Day} · Necrose · {NoticeRequests.Venue}, {NoticeRequests.CityName}";
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DailyDigest, ct);

        await runs.RunOnceAsync(ct);
        await runs.RunOnceAsync(ct);

        var cityMail = await SingleDigestAsync(cityFanEmail, ct);
        var bandMail = await SingleDigestAsync(bandFanEmail, ct);
        cityMail.Text.ShouldContain(coveRasaLine, Case.Sensitive, cityMail.Text);
        cityMail.Text.ShouldContain(necroseLine, Case.Sensitive, cityMail.Text);
        bandMail.Text.ShouldContain(necroseLine, Case.Sensitive, bandMail.Text);
        bandMail.Text.ShouldNotContain(coveRasaLine, Case.Sensitive, "quem segue só a banda recebeu o show da cidade");
        foreach (var mail in new[] { cityMail, bandMail })
        {
            AgendaLink().IsMatch(mail.Text).ShouldBeTrue($"o e-mail não tem o link da agenda (https://localhost/):\n{mail.Text}");
            UnsubscribeLink().IsMatch(mail.Text).ShouldBeTrue($"o e-mail não tem o link de descadastro:\n{mail.Text}");
        }

        // A segunda execução não manda outro e-mail (a entrega sai pelo job de entregas, a cada 1 s).
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        (await mailpit.GetMessagesAsync(cityFanEmail)).Count.ShouldBe(1, "a segunda execução do resumo mandou outro e-mail");
        (await mailpit.GetMessagesAsync(bandFanEmail)).Count.ShouldBe(1, "a segunda execução do resumo mandou outro e-mail");
    }

    /// <summary>Espera o resumo de <paramref name="email"/> chegar e exige que seja um só.</summary>
    private async Task<MailpitMessage> SingleDigestAsync(string email, CancellationToken ct)
    {
        (await Timing.EventuallyAsync(async () => (await mailpit.GetMessagesAsync(email)).Count > 0, Arrival, ct))
            .ShouldBeTrue($"o resumo diário não chegou ao Mailpit para {email}");
        var message = (await mailpit.GetMessagesAsync(email)).ShouldHaveSingleItem($"mais de um e-mail para {email}");
        message.Subject.ShouldBe(Subject);
        return message;
    }

    /// <summary>O link da agenda (url "/" do aviso, absoluta pelo <c>App:PublicUrl</c>) sozinho no fim de uma linha.</summary>
    [GeneratedRegex(@"https://localhost/(?=\s|$)", RegexOptions.Multiline)]
    private static partial Regex AgendaLink();

    [GeneratedRegex(@"https://localhost/avisos/descadastrar\?token=[A-Za-z0-9._~%-]+")]
    private static partial Regex UnsubscribeLink();
}
