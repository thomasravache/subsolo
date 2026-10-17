using System.Reflection;
using ArchUnitNET.Loader;
using Shouldly;
using Subsolo.ArchitectureTests.ModularMonolith.SmtpSamples.Mailer.Infrastructure;
using Xunit;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0018 IT-05 (ADR-0014) — só o módulo Notifications usa as bibliotecas de SMTP: nenhum tipo dos outros módulos
/// nem dos blocos Common depende de <c>MailKit</c>/<c>MimeKit</c>; o canal de e-mail do Notifications.Infrastructure
/// usa o MailKit (a regra não passa no vazio); e um dublê que usa o MailKit fora do Notifications faz a regra falhar,
/// nomeando-a. Os cinco assemblies <c>Subsolo.Modules.Notifications.*</c> estão entre os avaliados.
/// </summary>
public sealed class NotificationsModuleRulesTests
{
    private static readonly string[] NotificationsAssemblies =
    [
        "Subsolo.Modules.Notifications.Application",
        "Subsolo.Modules.Notifications.Domain",
        "Subsolo.Modules.Notifications.Infrastructure",
        "Subsolo.Modules.Notifications.IntegrationEvents",
        "Subsolo.Modules.Notifications.Presentation",
    ];

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-05")]
    [Trait("Kind", "Integration")]
    public void NotificationsAssembliesAreEvaluatedByTheRules()
    {
        var names = ModularMonolithRules.ModuleAssemblies().Select(assembly => assembly.GetName().Name).ToList();

        foreach (var name in NotificationsAssemblies)
        {
            names.ShouldContain(name);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-05")]
    [Trait("Kind", "Integration")]
    public void OnlyNotificationsUsesSmtpLibraries()
    {
        var assemblies = ModularMonolithRules.SmtpRuleAssemblies().ToArray();
        assemblies.Select(assembly => assembly.GetName().Name).ShouldContain("Subsolo.Common.Infrastructure");

        Should.NotThrow(() => ModularMonolithRules.Check(ModularMonolithRules.SmtpOnlyInNotifications, assemblies));
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-05")]
    [Trait("Kind", "Integration")]
    public void NotificationsEmailChannelUsesMailKit()
    {
        Assembly[] modules = [.. ModularMonolithRules.ModuleAssemblies()];
        var architecture = new ArchLoader().LoadAssemblies(modules).Build();

        var users = architecture.Types
            .Where(type => type.Namespace.FullName.StartsWith("Subsolo.Modules.Notifications.Infrastructure", StringComparison.Ordinal))
            .Where(type => type.Dependencies.Any(dependency =>
                dependency.Target.Namespace.FullName.StartsWith("MailKit", StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToList();

        users.ShouldNotBeEmpty("nenhum tipo do Notifications.Infrastructure usa o MailKit (o envio por SMTP não existe)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-05")]
    [Trait("Kind", "Integration")]
    public void SmtpOutsideNotificationsFailsNamingTheRule()
    {
        var sample = typeof(DirectSmtpSender).Assembly;

        var failure = Should.Throw<Exception>(() =>
            ModularMonolithRules.Check(ModularMonolithRules.SmtpOnlyInNotifications, sample));

        failure.ShouldNotBeOfType<NotImplementedException>();
        failure.Message.ShouldContain(ModularMonolithRules.SmtpOnlyInNotifications, Case.Sensitive, failure.Message);
        failure.Message.ShouldContain(nameof(DirectSmtpSender), Case.Sensitive, failure.Message);
    }
}
