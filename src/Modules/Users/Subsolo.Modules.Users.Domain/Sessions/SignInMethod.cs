namespace Subsolo.Modules.Users.Domain.Sessions;

/// <summary>SPEC-0014 — método de entrada da sessão (gravado como <c>magic_link</c> ou <c>google</c>).</summary>
public enum SignInMethod
{
    MagicLink,
    Google,
}
