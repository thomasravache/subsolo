using System.Globalization;
using System.Text;

namespace Subsolo.Modules.Events.Domain.Slugs;

/// <summary>
/// SPEC-0017 — endereço (slug) gerado do nome de uma cidade ou estilo: sem acento, minúsculas, espaços → hífen, no
/// formato <see cref="Pattern"/> (ex.: "São José dos Pinhais" → <c>sao-jose-dos-pinhais</c>).
/// </summary>
public static class SlugGenerator
{
    /// <summary>Formato do slug na URL.</summary>
    public const string Pattern = "^[a-z0-9-]{2,60}$";

    /// <summary>Tamanho máximo do slug.</summary>
    public const int MaxLength = 60;

    /// <summary>
    /// O slug gerado de <paramref name="name"/>: tira os acentos, passa para minúsculas e troca cada sequência de
    /// caracteres fora de <c>[a-z0-9]</c> por um hífen, sem hífen nas pontas (pode sair vazio ou curto demais para o
    /// <see cref="Pattern"/>: quem chama confere).
    /// </summary>
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
