using System.Reflection;
using Subsolo.Modules.Events.Application;

namespace Subsolo.Events.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0024 (H23-05) — leitura de shows sempre vazia para os testes da agenda da SPEC-0008, no lugar do segundo
/// construtor de teste do <c>GetCityAgendaQueryHandler</c> (que some). Feita com <see cref="DispatchProxy"/>: toda
/// consulta do <see cref="IShowsReadModel"/> (inclusive as que vierem depois) devolve lista vazia ou nada, sem o dublê
/// precisar mudar quando a interface ganhar membros.
/// </summary>
public class EmptyShowsReadModel : DispatchProxy
{
    internal static IShowsReadModel Create() => Create<IShowsReadModel, EmptyShowsReadModel>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [Empty(resultType)]);
        }

        throw new NotSupportedException($"{targetMethod.Name}: o dublê vazio só responde consultas assíncronas.");
    }

    /// <summary>Lista vazia para <c>IReadOnlyList&lt;T&gt;</c>; o padrão do tipo (nulo) para o resto.</summary>
    private static object? Empty(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? Array.CreateInstance(type.GetGenericArguments()[0], 0)
            : type.IsValueType ? Activator.CreateInstance(type) : null;
}
