using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0031 — os membros de um tipo que o IL de um assembly referencia (tabela MemberRef dos metadados; copiado do
/// <c>NoticesModuleRulesTests</c> da SPEC-0030). O compilador só emite a referência de um membro usado, e a tabela não
/// depende de onde a chamada foi parar (num método <c>async</c>, o <c>MoveNext</c> da máquina de estados é struct em
/// Release e o ArchUnitNET não vê a dependência).
/// </summary>
internal static class MemberReferences
{
    public static List<string> CalledOn(Assembly assembly, string typeNamespace, string typeName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var members = new List<string>();
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (metadata.StringComparer.Equals(type.Namespace, typeNamespace)
                && metadata.StringComparer.Equals(type.Name, typeName))
            {
                members.Add(metadata.GetString(member.Name));
            }
        }

        return members;
    }

    /// <summary>O assembly do módulo com o nome <paramref name="name"/>.</summary>
    public static Assembly Module(string name) =>
        ModularMonolithRules.ModuleAssemblies().Single(assembly => assembly.GetName().Name == name);
}
