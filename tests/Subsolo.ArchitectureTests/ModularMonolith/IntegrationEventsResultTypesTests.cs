using Shouldly;
using Subsolo.Modules.ResultSample.IntegrationEvents;
using Xunit;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0018 IT-05 (Emenda 1; ADR-0001 esclarecido em 2026-10-17) — a regra
/// <see cref="ModularMonolithRules.IntegrationEventsDependencies"/> libera no IntegrationEvents só os tipos de resultado
/// de Common.Domain: o dublê que usa <c>Result</c>, <c>Result&lt;T&gt;</c>, <c>Error</c> e <c>ErrorType</c> não aparece
/// na falha, e o que expõe <c>Entity</c> e <c>DomainEvent</c> reprova, nomeando a regra e os dois tipos proibidos.
/// </summary>
public sealed class IntegrationEventsResultTypesTests
{
    [Fact]
    [Trait("Spec", "SPEC-0018:IT-05")]
    [Trait("Kind", "Integration")]
    public void DomainEntitiesStayForbiddenWhileResultTypesPass()
    {
        var samples = typeof(IResultSampleApi).Assembly;

        var failure = Should.Throw<Exception>(() =>
            ModularMonolithRules.Check(ModularMonolithRules.IntegrationEventsDependencies, samples));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain(ModularMonolithRules.IntegrationEventsDependencies, Case.Sensitive, failure.Message);
        failure.Message.ShouldContain("Subsolo.Common.Domain.Entity", Case.Sensitive, failure.Message);
        failure.Message.ShouldContain("Subsolo.Common.Domain.DomainEvent", Case.Sensitive, failure.Message);
        failure.Message.ShouldNotContain("ResultSample", Case.Sensitive, failure.Message);
    }
}
