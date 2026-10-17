using System.Reflection;
using Shouldly;
using Subsolo.Common.ArchitectureViolations;
using Xunit;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0007 IT-06 — as regras do ADR-0001 valem para todo assembly <c>Subsolo.Modules.*</c> (aqui, os módulos de
/// exemplo Template e TemplateB, que existem só nos testes) e falham, nomeando a regra, para o assembly de violação
/// de exemplo, que tem uma violação de cada regra.
/// </summary>
public sealed class ModularMonolithRulesTests
{
    private static readonly Assembly Violations = typeof(ArchitectureViolationsMarker).Assembly;

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-06")]
    [Trait("Kind", "Integration")]
    public void EveryModuleAssemblyBesideTheTestsIsEvaluated()
    {
        var names = ModularMonolithRules.ModuleAssemblies().Select(a => a.GetName().Name).ToList();

        names.ShouldBe(
            [
                "Subsolo.Modules.Events.Application",
                "Subsolo.Modules.Events.Domain",
                "Subsolo.Modules.Events.Infrastructure",
                "Subsolo.Modules.Events.IntegrationEvents",
                "Subsolo.Modules.Events.Presentation",
                "Subsolo.Modules.Template",
                "Subsolo.Modules.Template.IntegrationEvents",
                "Subsolo.Modules.TemplateB",
                "Subsolo.Modules.Users.Application",
                "Subsolo.Modules.Users.Domain",
                "Subsolo.Modules.Users.Infrastructure",
                "Subsolo.Modules.Users.IntegrationEvents",
                "Subsolo.Modules.Users.Presentation",
            ],
            ignoreOrder: true);
    }

    [Theory]
    [Trait("Spec", "SPEC-0007:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData(ModularMonolithRules.DomainIsolation)]
    [InlineData(ModularMonolithRules.ApplicationIsolation)]
    [InlineData(ModularMonolithRules.ModuleBoundaries)]
    [InlineData(ModularMonolithRules.IntegrationEventsDependencies)]
    [InlineData(ModularMonolithRules.NoModuleCycles)]
    public void ModulesFollowTheRule(string rule)
    {
        var modules = ModularMonolithRules.ModuleAssemblies().ToArray();

        Should.NotThrow(() => ModularMonolithRules.Check(rule, modules));
    }

    [Theory]
    [Trait("Spec", "SPEC-0007:IT-06")]
    [Trait("Kind", "Integration")]
    [InlineData(ModularMonolithRules.DomainIsolation)]
    [InlineData(ModularMonolithRules.ApplicationIsolation)]
    [InlineData(ModularMonolithRules.ModuleBoundaries)]
    [InlineData(ModularMonolithRules.IntegrationEventsDependencies)]
    [InlineData(ModularMonolithRules.NoModuleCycles)]
    public void ViolationSampleFailsNamingTheRule(string rule)
    {
        var failure = Should.Throw<Exception>(() => ModularMonolithRules.Check(rule, Violations));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain(rule, Case.Sensitive, failure.Message);
    }
}
