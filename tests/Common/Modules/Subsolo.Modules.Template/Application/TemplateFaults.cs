namespace Subsolo.Modules.Template.Application;

/// <summary>
/// Dublê de teste: injeta falhas no handler do evento de domínio (retry da outbox) e registra as tentativas.
/// Registrado como singleton pelo <c>AddTemplateModule</c>.
/// </summary>
public sealed class TemplateFaults
{
    private readonly Lock _gate = new();
    private int _failuresLeft;
    private int _failuresAfterPublishLeft;
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

    /// <summary>Faz as próximas <paramref name="times"/> chamadas do handler falharem com exceção, antes de publicar.</summary>
    public void FailNext(int times)
    {
        lock (_gate)
        {
            _failuresLeft = times;
        }
    }

    /// <summary>
    /// Faz as próximas <paramref name="times"/> chamadas do handler falharem com exceção logo depois de publicar o
    /// evento de integração (a publicação já aconteceu, mas a outbox registra a falha e tenta de novo).
    /// </summary>
    public void FailAfterPublishNext(int times)
    {
        lock (_gate)
        {
            _failuresAfterPublishLeft = times;
        }
    }

    /// <summary>Registra uma tentativa e diz se ela deve falhar antes de publicar.</summary>
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

    /// <summary>Diz se a tentativa atual deve falhar depois de publicar.</summary>
    internal bool ShouldFailAfterPublish()
    {
        lock (_gate)
        {
            if (_failuresAfterPublishLeft <= 0)
            {
                return false;
            }

            _failuresAfterPublishLeft--;
            return true;
        }
    }
}
