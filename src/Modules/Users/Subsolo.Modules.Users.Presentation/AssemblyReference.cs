using System.Reflection;

namespace Subsolo.Modules.Users.Presentation;

/// <summary>SPEC-0014 — o assembly dos endpoints do módulo Users, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
