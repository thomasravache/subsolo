using System.Reflection;

namespace Subsolo.Modules.Site.Presentation;

/// <summary>SPEC-0028 — o assembly dos endpoints do módulo Site, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
