namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 (ADR-0008) — exceção de log já mascarada, entregue aos provedores no lugar da original quando a
/// mensagem, a pilha ou o <c>ToString()</c> dela trazem e-mail: <see cref="Message"/>, <see cref="StackTrace"/> e
/// <see cref="ToString"/> devolvem o texto da original (tipo, exceções internas e pilha) mascarado.
/// </summary>
#pragma warning disable CA1032, RCS1194 // só existe como invólucro de uma exceção já lançada
internal sealed class RedactedException : Exception
#pragma warning restore CA1032, RCS1194
{
    private readonly string _text;
    private readonly string? _stackTrace;

    private RedactedException(Exception original, string message, string text)
        : base(message)
    {
        OriginalType = original.GetType();
        _text = text;
        _stackTrace = original.StackTrace is { } stack ? PersonalDataRedactor.MaskEmails(stack) : null;
        HResult = original.HResult;
        Source = original.Source;
    }

    /// <summary>Tipo da exceção original.</summary>
    public Type OriginalType { get; }

    public override string? StackTrace => _stackTrace;

    public override string ToString() => _text;

    /// <summary>A própria <paramref name="exception"/> quando nada precisa ser mascarado; senão, o invólucro mascarado.</summary>
    public static Exception? From(Exception? exception)
    {
        if (exception is null or RedactedException)
        {
            return exception;
        }

        var message = PersonalDataRedactor.TryMaskEmails(exception.Message, out var maskedMessage);
        var text = PersonalDataRedactor.TryMaskEmails(exception.ToString(), out var maskedText);
        return message || text
            ? new RedactedException(exception, maskedMessage, maskedText)
            : exception;
    }
}
