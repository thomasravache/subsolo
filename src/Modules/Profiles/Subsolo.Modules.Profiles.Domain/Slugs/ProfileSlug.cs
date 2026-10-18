using System.Globalization;
using System.Text;

namespace Subsolo.Modules.Profiles.Domain.Slugs;

/// <summary>
/// SPEC-0021 — slug do perfil gerado do nome, com a mesma regra do Events (SPEC-0017): sem acento, minúsculas, cada
/// sequência fora de <c>[a-z0-9]</c> vira um hífen, sem hífen nas pontas (ex.: "Coágulo  Sônico" →
/// <c>coagulo-sonico</c>). Pode sair vazio (nome sem letras nem dígitos): quem chama confere.
/// </summary>
public static class ProfileSlug
{
    /// <summary>Tamanho máximo do slug.</summary>
    public const int MaxLength = 80;

    public static string FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var slug = new StringBuilder(name.Length);
        var pendingHyphen = false;
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingHyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }

                pendingHyphen = false;
                slug.Append(lower);
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return slug.Length <= MaxLength ? slug.ToString() : slug.ToString(0, MaxLength).TrimEnd('-');
    }
}
