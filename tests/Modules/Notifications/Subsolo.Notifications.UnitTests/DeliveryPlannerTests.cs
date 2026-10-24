using Subsolo.Modules.Notifications.Application.Channels;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;
using Subsolo.Modules.Notifications.Domain.Preferences;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Notifications.UnitTests;

/// <summary>
/// SPEC-0030 UT-02 — o planejador de entregas de <c>EnqueueAsync</c>, com preferências variadas: um aviso
/// <c>new_show</c> vira uma entrega <c>WebPush</c> só para quem tem o show novo ligado e um aparelho inscrito (nunca
/// e-mail); <c>reminder</c> vai por push e/ou e-mail conforme <c>reminderPush</c>/<c>reminderEmail</c> e os canais
/// disponíveis; <c>digest</c> só por e-mail, com <c>dailyDigestEmail</c> e endereço; as entregas já registradas para a
/// mesma chave (usuário, canal) não se repetem, e nenhum par (usuário, canal) sai duas vezes.
/// </summary>
public sealed class DeliveryPlannerTests
{
    private static readonly Guid AllOn = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PushOffEmailOn = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid AllOnWithoutDevice = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid AllOnWithoutEmail = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid Defaults = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly Guid AllOff = Guid.Parse("00000000-0000-0000-0000-000000000006");

    private static readonly RecipientChannels[] Recipients =
    [
        new(AllOn, new NotificationPreferences(true, true, true, true), HasPushSubscription: true, HasEmail: true),
        new(PushOffEmailOn, new NotificationPreferences(false, true, false, true), HasPushSubscription: true, HasEmail: true),
        new(AllOnWithoutDevice, new NotificationPreferences(true, true, true, true), HasPushSubscription: false, HasEmail: true),
        new(AllOnWithoutEmail, new NotificationPreferences(true, true, true, true), HasPushSubscription: true, HasEmail: false),
        new(Defaults, NotificationPreferences.Default, HasPushSubscription: true, HasEmail: true),
        new(AllOff, new NotificationPreferences(false, false, false, false), HasPushSubscription: true, HasEmail: true),
    ];

    private static readonly HashSet<PlannedDelivery> Nothing = [];

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-02")]
    public void ANewShowGoesByPushOnlyToWhoWantsItAndHasADevice()
    {
        var planned = DeliveryPlanner.Plan(Notice("new_show"), Recipients, Nothing);

        planned.ShouldBe(
            [
                new PlannedDelivery(AllOn, NotificationChannel.WebPush),
                new PlannedDelivery(AllOnWithoutEmail, NotificationChannel.WebPush),
            ],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-02")]
    public void AReminderGoesByEachChannelThePreferencesAndTheAvailableChannelsAllow()
    {
        var planned = DeliveryPlanner.Plan(Notice("reminder"), Recipients, Nothing);

        planned.ShouldBe(
            [
                new PlannedDelivery(AllOn, NotificationChannel.WebPush),
                new PlannedDelivery(AllOn, NotificationChannel.Email),
                new PlannedDelivery(PushOffEmailOn, NotificationChannel.Email),
                new PlannedDelivery(AllOnWithoutDevice, NotificationChannel.Email),
                new PlannedDelivery(AllOnWithoutEmail, NotificationChannel.WebPush),
                new PlannedDelivery(Defaults, NotificationChannel.WebPush),
            ],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-02")]
    public void ADigestGoesByEmailOnlyToWhoOptedInAndHasAnAddress()
    {
        var planned = DeliveryPlanner.Plan(Notice("digest"), Recipients, Nothing);

        planned.ShouldBe(
            [
                new PlannedDelivery(AllOn, NotificationChannel.Email),
                new PlannedDelivery(PushOffEmailOn, NotificationChannel.Email),
                new PlannedDelivery(AllOnWithoutDevice, NotificationChannel.Email),
            ],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-02")]
    public void DeliveriesAlreadyRegisteredForTheKeyAreNotPlannedAgain()
    {
        HashSet<PlannedDelivery> already =
        [
            new(AllOn, NotificationChannel.WebPush),
            new(PushOffEmailOn, NotificationChannel.Email),
        ];

        var planned = DeliveryPlanner.Plan(Notice("reminder"), Recipients, already);

        planned.ShouldBe(
            [
                new PlannedDelivery(AllOn, NotificationChannel.Email),
                new PlannedDelivery(AllOnWithoutDevice, NotificationChannel.Email),
                new PlannedDelivery(AllOnWithoutEmail, NotificationChannel.WebPush),
                new PlannedDelivery(Defaults, NotificationChannel.WebPush),
            ],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-02")]
    public void ARepeatedRecipientIsPlannedOncePerChannel()
    {
        RecipientChannels[] repeated = [Recipients[0], Recipients[0]];

        var planned = DeliveryPlanner.Plan(Notice("reminder"), repeated, Nothing);

        planned.ShouldBe(
            [
                new PlannedDelivery(AllOn, NotificationChannel.WebPush),
                new PlannedDelivery(AllOn, NotificationChannel.Email),
            ],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:UT-02")]
    public void WhoTurnedEverythingOffGetsNothing()
    {
        foreach (var kind in new[] { "new_show", "reminder", "digest" })
        {
            DeliveryPlanner.Plan(Notice(kind), Recipients, Nothing)
                .ShouldNotContain(delivery => delivery.UserId == AllOff, $"{kind} planejado para quem desligou tudo");
        }
    }

    private static Notice Notice(string kind) =>
        new(kind, $"ut02-{kind}", "Título", "Corpo", "https://subsolo.test/aviso");
}
