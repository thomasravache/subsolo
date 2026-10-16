using System.Collections.Concurrent;

namespace Subsolo.Modules.Template.Application;

/// <summary>
/// Dublê de teste: conta as chamadas que chegam aos handlers de comando e consulta do módulo (depois dos decorators).
/// Registrado como singleton pelo <c>AddTemplateModule</c>.
/// </summary>
public sealed class TemplateHandlerCalls
{
    private readonly ConcurrentDictionary<string, int> _calls = new(StringComparer.Ordinal);

    /// <summary>Quantas vezes o handler da mensagem <paramref name="messageName"/> foi chamado.</summary>
    public int Count(string messageName) => _calls.GetValueOrDefault(messageName);

    internal void Record(string messageName) => _calls.AddOrUpdate(messageName, 1, (_, count) => count + 1);
}
