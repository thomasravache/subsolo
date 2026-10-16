using System.Reflection;
using Shouldly;
using Subsolo.Examples.ArchitectureSamples;
using Xunit;

namespace Subsolo.ArchitectureTests.HarnessExamples;

/// <summary>
/// SPEC-0005 IT-05 — avaliada sobre um assembly de exemplo cujo Domain referencia o Infrastructure,
/// <see cref="ArchitectureRules.ForModule"/> faz o teste de arquitetura falhar nomeando a dependência; no módulo de
/// exemplo correto, a mesma regra passa (a regra não falha por qualquer motivo).
/// </summary>
public sealed class ArchitectureRulesExampleTests
{
    private static readonly Assembly Samples = typeof(ArchitectureSamplesMarker).Assembly;

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-05")]
    [Trait("Kind", "Integration")]
    public void DomainDependingOnInfrastructureFailsNamingTheDependency()
    {
        var failure = Should.Throw<Exception>(
            () => ArchitectureRules.ForModule("Leaky", Samples).DomainMustNotDependOn("Infrastructure"));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain("Leaky.Domain.Show", Case.Sensitive, failure.Message);
        failure.Message.ShouldContain("Leaky.Infrastructure.ShowRecord", Case.Sensitive, failure.Message);
    }

    [Fact]
    [Trait("Spec", "SPEC-0005:IT-05")]
    [Trait("Kind", "Integration")]
    public void DomainWithoutForbiddenDependencyPasses()
    {
        Should.NotThrow(() => ArchitectureRules.ForModule("Tidy", Samples).DomainMustNotDependOn("Infrastructure"));
    }
}
