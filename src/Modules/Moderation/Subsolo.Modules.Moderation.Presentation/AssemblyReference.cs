using System.Reflection;

namespace Subsolo.Modules.Moderation.Presentation;

/// <summary>SPEC-0033 — o assembly dos endpoints do módulo Moderation, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
