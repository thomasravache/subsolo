using System.Reflection;

namespace Subsolo.Modules.Events.Presentation;

/// <summary>SPEC-0008 — o assembly dos endpoints do módulo Events, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
