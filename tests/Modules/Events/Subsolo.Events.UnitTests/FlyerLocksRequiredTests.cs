using System.Reflection;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CleanupFlyers;
using Subsolo.Modules.Events.Application.Features.UploadFlyer;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0031 H26-02 (item herdado da revisão da SPEC-0026) — o <see cref="IFlyerLocks"/> é obrigatório no construtor de
/// <see cref="UploadFlyerCommandHandler"/> e de <see cref="CleanupFlyersCommandHandler"/>: sem valor padrão nulo e não
/// anulável (a trava não pode faltar por esquecimento no registro); os testes unitários usam o dublê
/// <c>NoOpFlyerLocks</c>.
/// </summary>
public sealed class FlyerLocksRequiredTests
{
    public static TheoryData<Type> Handlers => new() { typeof(UploadFlyerCommandHandler), typeof(CleanupFlyersCommandHandler) };

    [Theory]
    [Trait("Spec", "SPEC-0031:H26-02")]
    [MemberData(nameof(Handlers))]
    public void TheFlyerLocksAreARequiredConstructorParameter(Type handler)
    {
        var constructors = handler.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var constructor in constructors)
        {
            var locks = constructor.GetParameters().SingleOrDefault(parameter => parameter.ParameterType == typeof(IFlyerLocks))
                .ShouldNotBeNull($"{handler.Name}: um construtor não recebe IFlyerLocks");
            locks.IsOptional.ShouldBeFalse($"{handler.Name}: IFlyerLocks tem valor padrão (é opcional)");
            locks.HasDefaultValue.ShouldBeFalse($"{handler.Name}: IFlyerLocks tem valor padrão");
            new NullabilityInfoContext().Create(locks).WriteState
                .ShouldBe(NullabilityState.NotNull, $"{handler.Name}: IFlyerLocks é anulável");
        }
    }
}
