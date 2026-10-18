using System.Reflection;

namespace Subsolo.Modules.Profiles.Presentation;

/// <summary>SPEC-0021 — o assembly dos endpoints do módulo Profiles, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
