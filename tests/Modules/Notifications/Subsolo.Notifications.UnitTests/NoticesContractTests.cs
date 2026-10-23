using System.Reflection;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0030 CT-01 (parte .NET) — as interfaces públicas do §6 no assembly <c>Subsolo.Modules.Notifications.IntegrationEvents</c>:
/// <see cref="INotificationsApi"/> com <c>SendEmailAsync</c> (SPEC-0018@1, inalterado), <c>Task&lt;Result&gt;
/// EnqueueAsync(IReadOnlyCollection&lt;Guid&gt; userIds, Notice notice, CancellationToken ct)</c>, <c>Task
/// ForgetUserAsync(Guid userId, CancellationToken ct)</c> e <c>Task&lt;object?&gt; ExportAsync(Guid userId,
/// CancellationToken ct)</c>; <see cref="Notice"/> como record selado <c>(string Kind, string DedupeKey, string Title,
/// string Body, string Url)</c>; e <see cref="IRecipientDirectory"/> com <c>Task&lt;IReadOnlyDictionary&lt;Guid,
/// string&gt;&gt; GetEmailsAsync(IReadOnlyCollection&lt;Guid&gt; userIds, CancellationToken ct)</c>.
/// </summary>
public sealed class NoticesContractTests
{
    private const string ContractAssembly = "Subsolo.Modules.Notifications.IntegrationEvents";
    private const string ContractNamespace = "Subsolo.Modules.Notifications.IntegrationEvents";

    [Fact]
    [Trait("Spec", "SPEC-0030:CT-01")]
    public void NoticeAndRecipientDirectoryArePublicInTheIntegrationEventsAssembly()
    {
        foreach (var type in new[] { typeof(Notice), typeof(IRecipientDirectory) })
        {
            type.Assembly.GetName().Name.ShouldBe(ContractAssembly, type.Name);
            type.Namespace.ShouldBe(ContractNamespace, type.Name);
            type.IsPublic.ShouldBeTrue($"{type.Name} não é público");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:CT-01")]
    public void NotificationsApiHasTheMethodsOfTheContract()
    {
        var api = typeof(INotificationsApi);
        api.GetMethods().Select(method => method.Name)
            .ShouldBe(["SendEmailAsync", "EnqueueAsync", "ForgetUserAsync", "ExportAsync"], ignoreOrder: true);

        ShouldHaveSignature(api, "EnqueueAsync", typeof(Task<Result>), (typeof(IReadOnlyCollection<Guid>), "userIds"), (typeof(Notice), "notice"), (typeof(CancellationToken), "ct"));
        ShouldHaveSignature(api, "ForgetUserAsync", typeof(Task), (typeof(Guid), "userId"), (typeof(CancellationToken), "ct"));
        ShouldHaveSignature(api, "ExportAsync", typeof(Task<object>), (typeof(Guid), "userId"), (typeof(CancellationToken), "ct"));

        var export = api.GetMethod("ExportAsync").ShouldNotBeNull();
        new NullabilityInfoContext().Create(export.ReturnParameter).GenericTypeArguments.ShouldHaveSingleItem()
            .ReadState.ShouldBe(NullabilityState.Nullable, "ExportAsync devolve null quando não há nada do usuário");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:CT-01")]
    public void NoticeIsASealedRecordWithTheContractMembers()
    {
        var type = typeof(Notice);
        type.IsSealed.ShouldBeTrue("Notice não é selado");
        type.GetMethod("<Clone>$").ShouldNotBeNull("Notice não é record");

        type.GetConstructors().ShouldHaveSingleItem().GetParameters().Select(p => (p.ParameterType, p.Name)).ShouldBe(
        [
            (typeof(string), "Kind"),
            (typeof(string), "DedupeKey"),
            (typeof(string), "Title"),
            (typeof(string), "Body"),
            (typeof(string), "Url"),
        ]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:CT-01")]
    public void RecipientDirectoryHasOnlyGetEmailsAsync()
    {
        var directory = typeof(IRecipientDirectory);
        directory.IsInterface.ShouldBeTrue();
        directory.GetMethods().ShouldHaveSingleItem().Name.ShouldBe("GetEmailsAsync");

        ShouldHaveSignature(directory, "GetEmailsAsync", typeof(Task<IReadOnlyDictionary<Guid, string>>), (typeof(IReadOnlyCollection<Guid>), "userIds"), (typeof(CancellationToken), "ct"));
    }

    private static void ShouldHaveSignature(Type type, string name, Type returnType, params (Type Type, string Name)[] parameters)
    {
        var method = type.GetMethod(name).ShouldNotBeNull($"{type.Name}.{name} não existe");
        method.ReturnType.ShouldBe(returnType, $"{type.Name}.{name}");
        method.GetParameters().Select(p => (p.ParameterType, p.Name ?? string.Empty)).ShouldBe(parameters, $"{type.Name}.{name}");
    }
}
