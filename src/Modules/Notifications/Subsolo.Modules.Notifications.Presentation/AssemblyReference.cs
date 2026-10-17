using System.Reflection;

namespace Subsolo.Modules.Notifications.Presentation;

/// <summary>SPEC-0018 — o assembly dos endpoints do módulo Notifications, para o <c>AddEndpoints</c> do registro do módulo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
