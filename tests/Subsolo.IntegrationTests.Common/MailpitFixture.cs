using Xunit;

namespace Subsolo.IntegrationTests.Common;

/// <summary>
/// SPEC-0005 — servidor SMTP de teste (Mailpit) num container efêmero. O código sob teste envia para
/// <see cref="SmtpHost"/>:<see cref="SmtpPort"/>; o teste lê as mensagens recebidas pela API do Mailpit.
/// </summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    public string SmtpHost => throw new NotImplementedException();

    public int SmtpPort => throw new NotImplementedException();

    /// <summary>Mensagens recebidas pelo Mailpit endereçadas a <paramref name="to"/>.</summary>
    public Task<IReadOnlyList<MailpitMessage>> GetMessagesAsync(string to) => throw new NotImplementedException();

    /// <summary>Apaga todas as mensagens recebidas.</summary>
    public Task ClearAsync() => throw new NotImplementedException();

    public ValueTask InitializeAsync() => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
