using System.Text.Json;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.Application.Features.DeleteAccount;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0021 IT-07 (§7.6, herança da revisão da SPEC-0019; guarda: passa antes da mudança) — <c>PublishUserDeleted</c>
/// publica <see cref="UserDeletedIntegrationEvent"/> com o mesmo <c>UserId</c> e o mesmo <c>Id</c> do evento de domínio
/// (uma nova tentativa da outbox publica o mesmo evento), a data dele em <c>DeletedAtUtc</c> e só ids (nenhum texto).
/// </summary>
public sealed class PublishUserDeletedTests
{
    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    public async Task PublishesTheIntegrationEventWithTheSameIdsAndTheDeletionDate()
    {
        var bus = new RecordingEventBus();
        var userId = Guid.CreateVersion7();
        var occurredOn = new DateTime(2026, 10, 18, 9, 15, 0, DateTimeKind.Utc);
        var domainEvent = new UserDeletedDomainEvent(userId) { OccurredOnUtc = occurredOn };

        await new PublishUserDeleted(bus).Handle(domainEvent, TestContext.Current.CancellationToken);

        var published = bus.Published.ShouldHaveSingleItem().ShouldBeOfType<UserDeletedIntegrationEvent>();
        published.UserId.ShouldBe(userId);
        published.Id.ShouldBe(domainEvent.Id, "uma nova tentativa da outbox publica o mesmo evento");
        published.OccurredOnUtc.ShouldBe(occurredOn);
        published.OccurredOnUtc.Kind.ShouldBe(DateTimeKind.Utc);
        published.DeletedAtUtc.ShouldBe(new DateTimeOffset(occurredOn));
        published.DeletedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    public async Task AnUnspecifiedKindIsTreatedAsUtc()
    {
        var bus = new RecordingEventBus();
        var occurredOn = new DateTime(2026, 10, 18, 9, 15, 0, DateTimeKind.Unspecified);

        await new PublishUserDeleted(bus).Handle(
            new UserDeletedDomainEvent(Guid.CreateVersion7()) { OccurredOnUtc = occurredOn }, TestContext.Current.CancellationToken);

        var published = bus.Published.ShouldHaveSingleItem().ShouldBeOfType<UserDeletedIntegrationEvent>();
        published.OccurredOnUtc.Kind.ShouldBe(DateTimeKind.Utc, "a outbox restaura a data sem o Kind");
        published.DeletedAtUtc.ShouldBe(new DateTimeOffset(DateTime.SpecifyKind(occurredOn, DateTimeKind.Utc)));
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    public async Task TheIntegrationEventCarriesOnlyIds()
    {
        var bus = new RecordingEventBus();

        await new PublishUserDeleted(bus).Handle(
            new UserDeletedDomainEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        var published = bus.Published.ShouldHaveSingleItem();
        typeof(UserDeletedIntegrationEvent).GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .ShouldBeEmpty("o evento de integração não leva texto (o e-mail não sai do módulo Users)");
        typeof(UserDeletedIntegrationEvent).GetProperties().Select(property => property.Name)
            .ShouldBe(["UserId", "DeletedAtUtc", "Id", "OccurredOnUtc"], ignoreOrder: true);
        JsonSerializer.Serialize(published, published.GetType()).ShouldNotContain("@");
    }

    private sealed class RecordingEventBus : IEventBus
    {
        private readonly List<IIntegrationEvent> _published = [];

        public IReadOnlyList<IIntegrationEvent> Published => _published;

        public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
            where T : IIntegrationEvent
        {
            _published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
