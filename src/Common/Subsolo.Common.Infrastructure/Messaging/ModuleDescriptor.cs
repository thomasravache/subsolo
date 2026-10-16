using System.Reflection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>
/// SPEC-0007 — o que a infraestrutura sabe de um módulo registrado com <c>AddModule</c>: o DbContext, os assemblies
/// do módulo, os eventos de domínio que ele levanta e os eventos de integração que ele consome (pela inbox).
/// </summary>
internal sealed class ModuleDescriptor
{
    public ModuleDescriptor(Type dbContextType, IReadOnlyList<Assembly> assemblies)
    {
        DbContextType = dbContextType;
        Assemblies = assemblies;
        var types = assemblies.SelectMany(assembly => assembly.GetTypes()).ToList();

        DomainEventTypes = types
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IDomainEvent)))
            .ToDictionary(MessageSerializer.TypeName, StringComparer.Ordinal);

        IntegrationEventTypes = types
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType &&
                               contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .Distinct()
            .ToDictionary(MessageSerializer.TypeName, StringComparer.Ordinal);
    }

    public Type DbContextType { get; }

    /// <summary>Nome curto do módulo (o nome do DbContext), usado nas identidades dos jobs.</summary>
    public string Name => DbContextType.Name;

    public IReadOnlyList<Assembly> Assemblies { get; }

    /// <summary>Eventos de domínio do módulo, por nome de tipo (como gravados na outbox).</summary>
    public IReadOnlyDictionary<string, Type> DomainEventTypes { get; }

    /// <summary>Eventos de integração que algum handler do módulo consome, por nome de tipo.</summary>
    public IReadOnlyDictionary<string, Type> IntegrationEventTypes { get; }

    /// <summary>O handler pertence a este módulo (outro módulo pode tratar o mesmo evento na própria inbox).</summary>
    public bool Owns(object handler) => Assemblies.Contains(handler.GetType().Assembly);
}
