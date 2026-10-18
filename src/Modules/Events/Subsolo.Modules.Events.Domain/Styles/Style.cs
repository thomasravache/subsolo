using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Domain.Styles;

/// <summary>
/// SPEC-0017 — estilo de metal usado nos shows e nos filtros (ex.: death, black): slug imutável, gerado do nome, e o
/// nome exibido, que pode ser renomeado.
/// </summary>
public sealed class Style : Entity
{
    private Style(Guid id, string slug, string name)
    {
        Id = id;
        Slug = slug;
        Name = name;
    }

    public Guid Id { get; private set; }

    /// <summary>Identificador do estilo (<c>^[a-z0-9-]{2,60}$</c>); não muda depois de criado.</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    public static Style Create(string slug, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Style(Guid.NewGuid(), slug, name);
    }

    /// <summary>Troca o nome exibido; o slug continua o mesmo.</summary>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
