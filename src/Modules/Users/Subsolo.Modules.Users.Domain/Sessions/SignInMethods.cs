namespace Subsolo.Modules.Users.Domain.Sessions;

/// <summary>SPEC-0014 — o código gravado e medido de cada <see cref="SignInMethod"/>.</summary>
public static class SignInMethods
{
    public const string MagicLink = "magic_link";

    public const string Google = "google";

    public static string ToCode(SignInMethod method) => method switch
    {
        SignInMethod.MagicLink => MagicLink,
        SignInMethod.Google => Google,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Método de entrada desconhecido."),
    };

    public static SignInMethod FromCode(string code) => code switch
    {
        MagicLink => SignInMethod.MagicLink,
        Google => SignInMethod.Google,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Método de entrada desconhecido."),
    };
}
