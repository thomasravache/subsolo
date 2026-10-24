using System.Reflection;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0018 CT-01 — o assembly <c>Subsolo.Modules.Notifications.IntegrationEvents</c> publica o contrato do §6 que a
/// SPEC-0016 consome: <see cref="INotificationsApi"/> com <c>Task&lt;Result&gt; SendEmailAsync(EmailRequest request,
/// CancellationToken cancellationToken)</c> (SPEC-0030 acrescenta os métodos de avisos sem mudar este), <see cref="EmailRequest"/> como record selado
/// <c>(string To, EmailTemplate Template, IReadOnlyDictionary&lt;string, string&gt; Data)</c>,
/// <see cref="EmailTemplate"/> só com <c>MagicLink</c> e os códigos de erro como constantes públicas.
/// </summary>
public sealed class NotificationsContractTests
{
    private const string ContractAssembly = "Subsolo.Modules.Notifications.IntegrationEvents";
    private const string ContractNamespace = "Subsolo.Modules.Notifications.IntegrationEvents";

    [Fact]
    [Trait("Spec", "SPEC-0018:CT-01")]
    public void ContractTypesArePublicInTheIntegrationEventsAssembly()
    {
        foreach (var type in new[] { typeof(INotificationsApi), typeof(EmailRequest), typeof(EmailTemplate), typeof(NotificationsErrors) })
        {
            type.Assembly.GetName().Name.ShouldBe(ContractAssembly, type.Name);
            type.Namespace.ShouldBe(ContractNamespace, type.Name);
            type.IsPublic.ShouldBeTrue($"{type.Name} não é público");
        }
    }

    /// <summary>
    /// SPEC-0030 (§8, compatibilidade): o <see cref="INotificationsApi"/> ganha os métodos de avisos (conferidos em
    /// <c>NoticesContractTests</c>), e o <c>SendEmailAsync</c> continua com a assinatura da SPEC-0018@1.
    /// </summary>
    [Fact]
    [Trait("Spec", "SPEC-0018:CT-01")]
    public void NotificationsApiKeepsSendEmailAsync()
    {
        typeof(INotificationsApi).IsInterface.ShouldBeTrue();
        var method = typeof(INotificationsApi).GetMethods().Where(m => m.Name == "SendEmailAsync").ShouldHaveSingleItem();

        method.Name.ShouldBe("SendEmailAsync");
        method.ReturnType.ShouldBe(typeof(Task<Result>));
        method.GetParameters().Select(p => (p.ParameterType, p.Name)).ShouldBe(
        [
            (typeof(EmailRequest), "request"),
            (typeof(CancellationToken), "cancellationToken"),
        ]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:CT-01")]
    public void EmailRequestIsASealedRecordWithTheContractMembers()
    {
        var type = typeof(EmailRequest);
        type.IsSealed.ShouldBeTrue("EmailRequest não é selado");
        type.GetMethod("<Clone>$").ShouldNotBeNull("EmailRequest não é record");

        var constructor = type.GetConstructors().ShouldHaveSingleItem();
        constructor.GetParameters().Select(p => (p.ParameterType, p.Name)).ShouldBe(
        [
            (typeof(string), "To"),
            (typeof(EmailTemplate), "Template"),
            (typeof(IReadOnlyDictionary<string, string>), "Data"),
        ]);
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (p.PropertyType, p.Name)).ShouldBe(
            [
                (typeof(string), "To"),
                (typeof(EmailTemplate), "Template"),
                (typeof(IReadOnlyDictionary<string, string>), "Data"),
            ],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:CT-01")]
    public void EmailTemplateHasOnlyMagicLink()
    {
        typeof(EmailTemplate).IsEnum.ShouldBeTrue();
        Enum.GetNames<EmailTemplate>().ShouldBe(["MagicLink"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0018:CT-01")]
    public void ErrorCodesArePublicConstantsWithTheContractValues()
    {
        var type = typeof(NotificationsErrors);
        (type.IsAbstract && type.IsSealed).ShouldBeTrue("NotificationsErrors não é estática");

        var constants = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToDictionary(field => field.Name, field => (string?)field.GetRawConstantValue(), StringComparer.Ordinal);

        constants.ShouldBe(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["InvalidRecipient"] = "notifications.invalid_recipient",
                ["InvalidData"] = "notifications.invalid_data",
                ["EmailUnavailable"] = "notifications.email_unavailable",
            },
            ignoreOrder: true);
    }
}
