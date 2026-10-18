using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Domain.Flyers;

/// <summary>
/// SPEC-0022 — flyer enviado: o id é o SHA-256 (hexadecimal, 64 caracteres) do arquivo original, e as variantes ficam em
/// <c>flyers/&lt;id&gt;-thumb.webp</c>, <c>-full.webp</c> e <c>-og.jpg</c>. Até ser ligado a um show
/// (<see cref="AttachedAtUtc"/>), pode ser apagado pela limpeza. <see cref="UploadedBy"/> só serve para auditoria e limpeza.
/// </summary>
public sealed class Flyer : Entity
{
    /// <summary>Tamanho do id (SHA-256 em hexadecimal).</summary>
    public const int IdLength = 64;

    /// <summary>Diretório dos flyers, relativo ao <c>Media:Root</c>.</summary>
    public const string Folder = "flyers";

    /// <summary>Arquivo do <c>thumb</c> (400 px de largura, WebP).</summary>
    public const string Thumb = "thumb.webp";

    /// <summary>Arquivo do <c>full</c> (1080 px de largura, WebP).</summary>
    public const string Full = "full.webp";

    /// <summary>Arquivo do <c>og</c> (1200×630, JPEG).</summary>
    public const string Og = "og.jpg";

    private Flyer(string id, Guid uploadedBy, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UploadedBy = uploadedBy;
        CreatedAtUtc = createdAtUtc;
    }

    public string Id { get; private set; }

    public Guid UploadedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? AttachedAtUtc { get; private set; }

    public static Flyer Create(string id, Guid uploadedBy, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length != IdLength || !id.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("O id do flyer é o SHA-256 em hexadecimal minúsculo.", nameof(id));
        }

        return new Flyer(id, uploadedBy, createdAtUtc);
    }

    /// <summary>Caminho da variante <paramref name="file"/> (<see cref="Thumb"/>, <see cref="Full"/> ou <see cref="Og"/>) de <paramref name="id"/>, relativo ao <c>Media:Root</c>.</summary>
    public static string VariantPath(string id, string file) => $"{Folder}/{id}-{file}";

    /// <summary>Os caminhos das três variantes (relativos ao <c>Media:Root</c>).</summary>
    public static IReadOnlyList<string> VariantPaths(string id) => [VariantPath(id, Thumb), VariantPath(id, Full), VariantPath(id, Og)];

    /// <summary>
    /// SPEC-0023 (H22-01) — o mesmo arquivo foi enviado de novo: se o flyer ainda não está ligado a um show, a contagem
    /// das 24 h da limpeza recomeça em <paramref name="nowUtc"/>.
    /// </summary>
    public void Renew(DateTimeOffset nowUtc)
    {
        if (AttachedAtUtc is null && nowUtc > CreatedAtUtc)
        {
            CreatedAtUtc = nowUtc;
        }
    }

    /// <summary>Liga o flyer a um show (a primeira ligação vale; ligar de novo não muda a data).</summary>
    public void Attach(DateTimeOffset attachedAtUtc) => AttachedAtUtc ??= attachedAtUtc;
}
