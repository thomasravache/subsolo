namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0032 H31-01 (contrato v2: resumo diário com recuperação) — o runbook do alerta "Job agendado falhou" (SPEC-0036 H32-01; antes "Resumo diário falhou")
/// (<c>docs/runbooks/outbox-parada.md</c>) descreve a recuperação: cita o gatilho <c>Engagement:DigestCatchUpCron</c> e a
/// tabela <c>engagement.digest_runs</c> que diz se o resumo do dia já saiu.
/// </summary>
public sealed class DigestCatchUpRunbookTests
{
    [Theory]
    [Trait("Spec", "SPEC-0032:H31-01")]
    [InlineData("DigestCatchUpCron")]
    [InlineData("digest_runs")]
    public void TheOutboxRunbookDescribesTheDigestCatchUp(string term)
    {
        var runbook = Path.Combine(RepositoryRoot(), "docs", "runbooks", "outbox-parada.md");

        File.ReadAllText(runbook).ShouldContain(term, Case.Sensitive, $"o runbook outbox-parada.md não cita {term}");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Subsolo.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }
}
