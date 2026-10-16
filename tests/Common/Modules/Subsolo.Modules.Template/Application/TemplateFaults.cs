namespace Subsolo.Modules.Template.Application;

/// <summary>
/// Dublê de teste: injeta falhas no handler do evento de domínio (retry da outbox) e registra as tentativas.
/// Registrado como singleton pelo <c>AddTemplateModule</c>.
/// </summary>
public sealed class TemplateFaults
{
    private readonly Lock _gate = new();
    private int _failuresLeft;
    private int _attempts;

    /// <summary>Quantas vezes o handler foi chamado (falhas e sucesso).</summary>
    public int Attempts
    {
        get
        {
            lock (_gate)
            {
                return _attempts;
            }
        }
    }

    /// <summary>Faz as próximas <paramref name="times"/> chamadas do handler falharem com exceção.</summary>
    public void FailNext(int times)
    {
        lock (_gate)
        {
            _failuresLeft = times;
        }
    }

    /// <summary>Registra uma tentativa e diz se ela deve falhar.</summary>
    internal bool RegisterAttemptAndShouldFail()
    {
        lock (_gate)
        {
            _attempts++;
            if (_failuresLeft <= 0)
            {
                return false;
            }

            _failuresLeft--;
            return true;
        }
    }
}
