using System.Reflection;

namespace Subsolo.Modules.Engagement.Presentation;

/// <summary>SPEC-0027 — o assembly dos endpoints do módulo Engagement, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
