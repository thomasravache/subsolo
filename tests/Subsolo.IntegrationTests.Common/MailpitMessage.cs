namespace Subsolo.IntegrationTests.Common;

/// <summary>SPEC-0005 — mensagem recebida pelo Mailpit.</summary>
public sealed record MailpitMessage(string Id, string From, IReadOnlyList<string> To, string Subject, string Text);
