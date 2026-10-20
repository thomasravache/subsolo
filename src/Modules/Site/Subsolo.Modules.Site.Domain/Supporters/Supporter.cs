using Subsolo.Common.Domain;

namespace Subsolo.Modules.Site.Domain.Supporters;

/// <summary>
/// SPEC-0028 — apoiador da página Apoie: nome público (dado pela própria pessoa ou coletivo), link <c>https</c>
/// opcional e o momento do cadastro, que dá a ordem da lista.
/// </summary>
public sealed class Supporter : Entity
{
    public const int NameMinLength = 2;

    public const int NameMaxLength = 60;

    /// <summary>Tamanho máximo do link (o suficiente para qualquer página de banda, selo ou zine).</summary>
    public const int UrlMaxLength = 300;

    private Supporter(Guid id, string name, string? url, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        Url = url;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Url { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Um apoiador novo (id versão 7, ordenável pelo momento da criação).</summary>
    public static Supporter Create(string name, string? url, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new Supporter(Guid.CreateVersion7(createdAtUtc), name, url, createdAtUtc);
    }
}
