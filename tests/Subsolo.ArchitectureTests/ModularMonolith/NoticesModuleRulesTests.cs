using System.Reflection;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0030 IT-07 (ADR-0001, ADR-0014) — com os avisos no módulo Notifications:
/// <list type="bullet">
/// <item>o Notifications não referencia nenhum outro módulo (nem o IntegrationEvents deles): o e-mail vem pelo
/// <c>IRecipientDirectory</c>, que mora no contrato do Notifications e é implementado pelo Users;</item>
/// <item>a biblioteca de Web Push só aparece no Notifications (<see cref="ModularMonolithRules.PushOnlyInNotifications"/>),
/// e o Notifications.Infrastructure a usa (a regra não passa no vazio);</item>
/// <item>o Users chama <c>INotificationsApi.ForgetUserAsync</c> (exclusão) e <c>ExportAsync</c> (seção do export);</item>
/// <item>H28-06 — o endpoint novo valida com o <c>EndpointValidation</c> do Common.Presentation.</item>
/// </list>
/// </summary>
public sealed class NoticesModuleRulesTests
{
    private const string RecipientDirectory = "Subsolo.Modules.Notifications.IntegrationEvents.IRecipientDirectory";
    private const string NotificationsApi = "Subsolo.Modules.Notifications.IntegrationEvents.INotificationsApi";

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-07")]
    [Trait("Kind", "Integration")]
    public void NotificationsReferencesNoOtherModule()
    {
        var architecture = Load();

        var rule = Types().That().ResideInNamespaceMatching(@"^Subsolo\.Modules\.Notifications(?:\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Subsolo\.Modules\.(?!Notifications(?:\.|$))[^.]+(?:\..*)?$")
            .Because("ADR-0001/ADR-0014: o Notifications não depende de módulo de domínio (nem do IntegrationEvents deles)")
            .WithoutRequiringPositiveResults();

        Should.NotThrow(() => rule.Check(architecture));
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-07")]
    [Trait("Kind", "Integration")]
    public void OnlyNotificationsUsesThePushLibrary()
    {
        var assemblies = ModularMonolithRules.SmtpRuleAssemblies().ToArray();

        Should.NotThrow(() => ModularMonolithRules.Check(ModularMonolithRules.PushOnlyInNotifications, assemblies));
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-07")]
    [Trait("Kind", "Integration")]
    public void NotificationsInfrastructureUsesThePushLibrary()
    {
        var architecture = Load();
        var pushNamespace = new System.Text.RegularExpressions.Regex(ModularMonolithRules.PushLibraryNamespaces);

        var users = architecture.Types
            .Where(type => type.Namespace.FullName.StartsWith("Subsolo.Modules.Notifications.Infrastructure", StringComparison.Ordinal))
            .Where(type => type.Dependencies.Any(dependency =>
                dependency.Target?.Namespace?.FullName is { } target && pushNamespace.IsMatch(target)))
            .Select(type => type.FullName)
            .ToList();

        users.ShouldNotBeEmpty("nenhum tipo do Notifications.Infrastructure usa a biblioteca de Web Push (o canal WebPush não existe)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-07")]
    [Trait("Kind", "Integration")]
    public void TheRecipientDirectoryLivesInTheNotificationsContractAndIsImplementedByUsers()
    {
        var types = ModularMonolithRules.ModuleAssemblies().SelectMany(assembly => assembly.GetTypes()).ToList();

        var directory = types.Where(type => type.FullName == RecipientDirectory)
            .ShouldHaveSingleItem("IRecipientDirectory não encontrado (ou duplicado)");
        directory.IsInterface.ShouldBeTrue();
        directory.Assembly.GetName().Name.ShouldBe("Subsolo.Modules.Notifications.IntegrationEvents");

        var implementations = types
            .Where(type => type is { IsClass: true, IsAbstract: false } && directory.IsAssignableFrom(type))
            .ToList();
        implementations.ShouldNotBeEmpty("ninguém implementa IRecipientDirectory");
        implementations.ShouldAllBe(
            type => type.Namespace != null && type.Namespace.StartsWith("Subsolo.Modules.Users.", StringComparison.Ordinal),
            $"IRecipientDirectory implementado fora do Users: {string.Join(", ", implementations.Select(type => type.FullName))}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-07")]
    [Trait("Kind", "Integration")]
    public void NotificationsReadsTheEmailsThroughTheRecipientDirectory()
    {
        var architecture = Load();

        var uses = architecture.Types
            .Where(type => type.Namespace?.FullName?.StartsWith("Subsolo.Modules.Notifications.", StringComparison.Ordinal) == true)
            .Where(type => !type.Namespace.FullName.StartsWith("Subsolo.Modules.Notifications.IntegrationEvents", StringComparison.Ordinal))
            .Any(type => type.Dependencies.Any(dependency => dependency.Target?.FullName == RecipientDirectory));

        uses.ShouldBeTrue("o Notifications não usa o IRecipientDirectory para o e-mail dos destinatários");
    }

    [Theory]
    [Trait("Spec", "SPEC-0030:IT-07")]
    [Trait("Kind", "Integration")]
    [InlineData("ForgetUserAsync", "a exclusão da conta apaga os dados de avisos")]
    [InlineData("ExportAsync", "o export da conta inclui a seção notifications")]
    public void UsersCallsTheNotificationsApi(string method, string reason)
    {
        var architecture = Load();

        var calls = architecture.Types
            .Where(type => type.Namespace?.FullName?.StartsWith("Subsolo.Modules.Users.", StringComparison.Ordinal) == true)
            .Any(type => type.Dependencies.OfType<MethodCallDependency>().Any(call =>
                call.TargetMember.DeclaringType.FullName == NotificationsApi
                && call.TargetMember.Name.StartsWith($"{method}(", StringComparison.Ordinal)));

        calls.ShouldBeTrue($"SPEC-0030: o Users não chama INotificationsApi.{method} ({reason})");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:H28-06")]
    [Trait("Kind", "Integration")]
    public void TheNewEndpointValidatesWithTheCommonEndpointValidation()
    {
        var presentation = ModularMonolithRules.ModuleAssemblies()
            .Single(assembly => assembly.GetName().Name == "Subsolo.Modules.Notifications.Presentation");

        var calls = MembersCalledOn(presentation, "Subsolo.Common.Presentation", "EndpointValidation");

        calls.ShouldNotBeEmpty("H28-06: o endpoint de inscrição não usa o EndpointValidation do Common.Presentation");
    }

    /// <summary>
    /// Os membros de <paramref name="typeNamespace"/>.<paramref name="typeName"/> que o IL de <paramref name="assembly"/>
    /// referencia (tabela MemberRef dos metadados). O compilador só emite a referência de um membro usado, e a tabela
    /// não depende de onde a chamada foi parar: num método <c>async</c> ela fica no <c>MoveNext</c> da máquina de estados,
    /// que é classe em Debug e struct em Release (o ArchUnitNET não vê a dependência no segundo caso).
    /// </summary>
    private static List<string> MembersCalledOn(Assembly assembly, string typeNamespace, string typeName)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        var members = new List<string>();
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference)
            {
                continue;
            }

            var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
            if (metadata.StringComparer.Equals(type.Namespace, typeNamespace)
                && metadata.StringComparer.Equals(type.Name, typeName))
            {
                members.Add(metadata.GetString(member.Name));
            }
        }

        return members;
    }

    private static ArchUnitNET.Domain.Architecture Load()
    {
        Assembly[] assemblies =
        [
            .. ModularMonolithRules.ModuleAssemblies(),
            typeof(Subsolo.Common.Presentation.IEndpoint).Assembly,
        ];
        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }
}
