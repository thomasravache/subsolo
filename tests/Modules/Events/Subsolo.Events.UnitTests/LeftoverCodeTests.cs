using System.Reflection;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CleanupFlyers;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0024 H23-05 (revisão da SPEC-0023) — as sobras somem: a limpeza dos flyers não recebe mais a unidade de trabalho
/// que nunca usou, o handler da agenda fica com um construtor só (o segundo, sem leitura de shows, era só para os testes
/// da SPEC-0008, que agora usam o <see cref="Infrastructure.EmptyShowsReadModel"/>), e o repositório de flyers não tem
/// mais o <c>ListUnattachedCreatedBeforeAsync</c>, sem uso desde a exclusão condicional (H22-01).
/// </summary>
public sealed class LeftoverCodeTests
{
    private const BindingFlags Constructors = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-05")]
    public void TheFlyerCleanupNoLongerTakesTheUnusedUnitOfWork()
    {
        var withUnitOfWork = typeof(CleanupFlyersCommandHandler)
            .GetConstructors(Constructors)
            .Where(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IEventsUnitOfWork)))
            .Select(Describe)
            .ToList();

        withUnitOfWork.ShouldBeEmpty("a limpeza ainda tem construtor com o IEventsUnitOfWork sem uso");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-05")]
    public void TheAgendaHandlerHasASingleConstructor()
    {
        var constructors = typeof(GetCityAgendaQueryHandler).GetConstructors(Constructors).Select(Describe).ToList();

        constructors.ShouldHaveSingleItem($"o handler da agenda tem mais de um construtor: {string.Join("; ", constructors)}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-05")]
    public void TheFlyerRepositoryNoLongerListsTheUnattachedFlyers()
    {
        typeof(IFlyerRepository).GetMethod("ListUnattachedCreatedBeforeAsync")
            .ShouldBeNull("IFlyerRepository.ListUnattachedCreatedBeforeAsync continua lá, sem uso");
    }

    private static string Describe(ConstructorInfo constructor) =>
        $"({string.Join(", ", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name))})";
}
