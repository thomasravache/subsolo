using System.Reflection;
using ArchUnitNET.Loader;
using Shouldly;
using Subsolo.ArchitectureTests.ModularMonolith.MediaSamples.Shows.Application;
using Subsolo.ArchitectureTests.ModularMonolith.MediaSamples.Shows.Infrastructure;
using Subsolo.Common.Application.Media;
using Xunit;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0022 IT-08 (ADR-0015) — só o <c>Common.Infrastructure</c> referencia o NetVips (nenhum tipo dos módulos, dos
/// outros blocos Common nem dos hosts depende dele) e é ele quem processa o flyer com o NetVips, implementando
/// <see cref="IImageProcessor"/> e <see cref="IFileStorage"/> (as regras não passam no vazio); os módulos e os blocos
/// Common não mexem no sistema de arquivos direto, e o <c>Events</c> usa <see cref="IFileStorage"/> e
/// <see cref="IImageProcessor"/>. Dublês que usam o NetVips ou o <c>System.IO.File</c> fora do Common.Infrastructure fazem
/// as regras falharem, nomeando-as.
/// </summary>
public sealed class MediaModuleRulesTests
{
    [Fact]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Kind", "Integration")]
    public void OnlyCommonInfrastructureUsesNetVips()
    {
        var assemblies = ModularMonolithRules.NetVipsRuleAssemblies().ToArray();
        var names = assemblies.Select(assembly => assembly.GetName().Name).ToList();
        names.ShouldContain("Subsolo.Common.Infrastructure");
        names.ShouldContain("Subsolo.Common.Application");
        names.ShouldContain("Subsolo.Api");
        names.ShouldContain("Subsolo.Modules.Events.Infrastructure");

        Should.NotThrow(() => ModularMonolithRules.Check(ModularMonolithRules.NetVipsOnlyInCommonInfrastructure, assemblies));
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Kind", "Integration")]
    public void CommonInfrastructureImplementsTheImageProcessorWithNetVipsAndTheFileStorage()
    {
        var infrastructure = Assembly.Load("Subsolo.Common.Infrastructure");
        var architecture = new ArchLoader().LoadAssemblies(infrastructure).Build();

        var netVipsUsers = architecture.Types
            .Where(type => type.Dependencies.Any(dependency =>
                dependency.Target.Namespace?.FullName.StartsWith("NetVips", StringComparison.Ordinal) == true))
            .Select(type => type.FullName)
            .ToList();
        netVipsUsers.ShouldNotBeEmpty("nenhum tipo do Common.Infrastructure usa o NetVips (o processamento do flyer não existe)");

        var types = infrastructure.GetTypes();
        types.ShouldContain(
            type => !type.IsAbstract && typeof(IImageProcessor).IsAssignableFrom(type),
            "o Common.Infrastructure não implementa IImageProcessor");
        types.ShouldContain(
            type => !type.IsAbstract && typeof(IFileStorage).IsAssignableFrom(type),
            "o Common.Infrastructure não implementa IFileStorage (disco em Media:Root)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Kind", "Integration")]
    public void ModulesAndCommonBlocksDoNotTouchTheFileSystemDirectly()
    {
        var assemblies = ModularMonolithRules.MediaRuleAssemblies().ToArray();
        var names = assemblies.Select(assembly => assembly.GetName().Name).ToList();
        names.ShouldContain("Subsolo.Modules.Events.Infrastructure");
        names.ShouldContain("Subsolo.Common.Application");
        names.ShouldNotContain("Subsolo.Api", "o host serve /media do disco no modo de desenvolvimento");

        Should.NotThrow(() => ModularMonolithRules.Check(ModularMonolithRules.MediaOnlyThroughFileStorage, assemblies));
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Kind", "Integration")]
    public void EventsUsesTheFileStorageAndTheImageProcessor()
    {
        Assembly[] events = [.. ModularMonolithRules.ModuleAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Subsolo.Modules.Events.", StringComparison.Ordinal) == true)];
        var architecture = new ArchLoader().LoadAssemblies(events).Build();

        foreach (var contract in new[] { typeof(IFileStorage), typeof(IImageProcessor) })
        {
            architecture.Types
                .Where(type => type.Dependencies.Any(dependency => dependency.Target.FullName == contract.FullName))
                .Select(type => type.FullName)
                .ShouldNotBeEmpty($"nenhum tipo do Events usa {contract.Name} (o upload de flyer não passa pela abstração)");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Kind", "Integration")]
    public void NetVipsOutsideCommonInfrastructureFailsNamingTheRule()
    {
        var failure = Should.Throw<Exception>(() =>
            ModularMonolithRules.Check(ModularMonolithRules.NetVipsOnlyInCommonInfrastructure, typeof(DirectFlyerResizer).Assembly));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain(ModularMonolithRules.NetVipsOnlyInCommonInfrastructure, Case.Sensitive, failure.Message);
        failure.Message.ShouldContain(nameof(DirectFlyerResizer), Case.Sensitive, failure.Message);
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Kind", "Integration")]
    public void FileSystemOutsideCommonInfrastructureFailsNamingTheRule()
    {
        var failure = Should.Throw<Exception>(() =>
            ModularMonolithRules.Check(ModularMonolithRules.MediaOnlyThroughFileStorage, typeof(DirectFlyerWriter).Assembly));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain(ModularMonolithRules.MediaOnlyThroughFileStorage, Case.Sensitive, failure.Message);
        failure.Message.ShouldContain(nameof(DirectFlyerWriter), Case.Sensitive, failure.Message);
    }
}
