using System.Net.Mail;

namespace Subsolo.Examples.IntegrationTests;

/// <summary>
/// SPEC-0005 IT-02 — um e-mail enviado por SMTP ao <see cref="MailpitFixture"/> (servidor SMTP de teste em
/// container) é recuperável por destinatário, com o assunto enviado.
/// </summary>
public sealed class MailpitFixtureTests(MailpitFixture mail) : IClassFixture<MailpitFixture>
{
    [Fact]
    [Trait("Spec", "SPEC-0005:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task MessageSentBySmtpIsRetrievableByRecipient()
    {
        var ct = TestContext.Current.CancellationToken;
        await mail.ClearAsync();
        var recipient = $"fa-{Guid.NewGuid():N}@subsolo.test";
        var otherRecipient = $"outro-{Guid.NewGuid():N}@subsolo.test";
        const string Subject = "SPEC-0005 IT-02: show novo na sua cidade";

        using (var smtp = new SmtpClient(mail.SmtpHost, mail.SmtpPort))
        {
            using var message = new MailMessage("avisos@subsolo.test", recipient, Subject, "Corpo de teste.");
            await smtp.SendMailAsync(message, ct);
            using var unrelated = new MailMessage("avisos@subsolo.test", otherRecipient, "Outro assunto", "Outro corpo.");
            await smtp.SendMailAsync(unrelated, ct);
        }

        var messages = await mail.GetMessagesAsync(recipient);

        var received = messages.ShouldHaveSingleItem();
        received.Subject.ShouldBe(Subject);
        received.To.ShouldContain(recipient);
    }
}
