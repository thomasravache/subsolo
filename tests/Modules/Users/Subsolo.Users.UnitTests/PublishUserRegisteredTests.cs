using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.Application.Features.RegisterUser;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0016 UT-06 (§7.6 c, e; achados da revisão da SPEC-0014) — <c>PublishUserRegistered</c> publica
/// <see cref="UserRegisteredIntegrationEvent"/> com o mesmo <c>UserId</c> e o mesmo <c>Id</c> do evento de domínio, a
/// data dele e nenhum e-mail; o <see cref="SignInService"/> exige <see cref="ILogger"/> (sem parâmetro opcional).
/// </summary>
public sealed class PublishUserRegisteredTests
{
    [Fact]
    [Trait("Spec", "SPEC-0016:UT-06")]
    public async Task PublishesTheIntegrationEventWithTheSameIdsAndDate()
    {
        var bus = new RecordingEventBus();
        var userId = Guid.CreateVersion7();
        var occurredOn = new DateTime(2026, 10, 17, 12, 30, 0, DateTimeKind.Utc);
        var domainEvent = new UserRegisteredDomainEvent(userId) { OccurredOnUtc = occurredOn };

        await new PublishUserRegistered(bus).Handle(domainEvent, TestContext.Current.CancellationToken);

        var published = bus.Published.ShouldHaveSingleItem().ShouldBeOfType<UserRegisteredIntegrationEvent>();
        published.UserId.ShouldBe(userId);
        published.Id.ShouldBe(domainEvent.Id, "uma nova tentativa da outbox publica o mesmo evento");
        published.OccurredOnUtc.ShouldBe(occurredOn);
        published.OccurredOnUtc.Kind.ShouldBe(DateTimeKind.Utc);
        published.RegisteredAtUtc.ShouldBe(new DateTimeOffset(occurredOn));
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-06")]
    public async Task TheIntegrationEventCarriesNoEmail()
    {
        var bus = new RecordingEventBus();
        await new PublishUserRegistered(bus).Handle(
            new UserRegisteredDomainEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        var published = bus.Published.ShouldHaveSingleItem();
        typeof(UserRegisteredIntegrationEvent).GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .ShouldBeEmpty("o evento de integração não leva texto (o e-mail não sai do módulo Users)");
        JsonSerializer.Serialize(published, published.GetType()).ShouldNotContain("@");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-06")]
    public void SignInServiceRequiresALogger()
    {
        var constructor = typeof(SignInService).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldHaveSingleItem();
        var logger = constructor.GetParameters()
            .Where(parameter => typeof(ILogger).IsAssignableFrom(parameter.ParameterType))
            .ShouldHaveSingleItem("o SignInService recebe um ILogger");

        logger.IsOptional.ShouldBeFalse("o ILogger do SignInService é obrigatório (sem valor padrão)");
        logger.HasDefaultValue.ShouldBeFalse("o ILogger do SignInService é obrigatório (sem valor padrão)");
        new NullabilityInfoContext().Create(logger).WriteState
            .ShouldBe(NullabilityState.NotNull, "o ILogger do SignInService não é anulável");
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
