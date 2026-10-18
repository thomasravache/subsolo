using Shouldly;
using Xunit;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0007 IT-09 — todo projeto <c>Subsolo.Modules.*.csproj</c> do repositório (em <c>src/</c> e <c>tests/</c>)
/// está entre os assemblies avaliados pelas regras do ADR-0001; um módulo que ninguém referenciou no projeto de
/// testes de arquitetura faz o teste falhar com o nome dele.
/// </summary>
public sealed class EveryModuleLoadedTests
{
    [Fact]
    [Trait("Spec", "SPEC-0007:IT-09")]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void ModuleProjectsAreFoundUnderSrcAndTests()
    {
        var projects = ModularMonolithRules.ModuleProjects(RepositoryRoot());

        projects.ShouldBe(
            [
                "Subsolo.Modules.Events.Application",
                "Subsolo.Modules.Events.Domain",
                "Subsolo.Modules.Events.Infrastructure",
                "Subsolo.Modules.Events.IntegrationEvents",
                "Subsolo.Modules.Events.Presentation",
                "Subsolo.Modules.Notifications.Application",
                "Subsolo.Modules.Notifications.Domain",
                "Subsolo.Modules.Notifications.Infrastructure",
                "Subsolo.Modules.Notifications.IntegrationEvents",
                "Subsolo.Modules.Notifications.Presentation",
                "Subsolo.Modules.Profiles.Application",
                "Subsolo.Modules.Profiles.Domain",
                "Subsolo.Modules.Profiles.Infrastructure",
                "Subsolo.Modules.Profiles.IntegrationEvents",
                "Subsolo.Modules.Profiles.Presentation",
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

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-09")]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Kind", "Integration")]
    public void EveryModuleProjectOfTheRepositoryIsLoadedByTheRules()
    {
        Should.NotThrow(() =>
            ModularMonolithRules.CheckEveryModuleIsLoaded(RepositoryRoot(), ModularMonolithRules.ModuleAssemblies()));
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-09")]
    [Trait("Kind", "Integration")]
    public void ModuleProjectNotReferencedByTheTestsFailsNamingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"subsolo-it09-{Guid.NewGuid():N}");
        try
        {
            CreateProject(root, "src/Modules/Ghost/Subsolo.Modules.Ghost.Domain/Subsolo.Modules.Ghost.Domain.csproj");
            CreateProject(root, "tests/Modules/Subsolo.Modules.Phantom/Subsolo.Modules.Phantom.csproj");
            CreateProject(root, "tests/Common/Modules/Subsolo.Modules.Template/Subsolo.Modules.Template.csproj");

            var failure = Should.Throw<Exception>(() =>
                ModularMonolithRules.CheckEveryModuleIsLoaded(root, ModularMonolithRules.ModuleAssemblies()));

            failure.ShouldNotBeOfType<NotImplementedException>();
            failure.Message.ShouldContain("Subsolo.Modules.Ghost.Domain", Case.Sensitive, failure.Message);
            failure.Message.ShouldContain("Subsolo.Modules.Phantom", Case.Sensitive, failure.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateProject(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    }

    /// <summary>Raiz do repositório: a pasta acima do assembly de teste que contém o <c>Subsolo.slnx</c>.</summary>
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Subsolo.slnx não encontrado acima de {AppContext.BaseDirectory}.");
    }
}
