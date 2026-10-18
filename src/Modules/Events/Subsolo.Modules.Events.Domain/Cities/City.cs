using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Domain.Cities;

/// <summary>SPEC-0008 — cidade com agenda própria: slug da URL (<c>/{cidade}</c>), nome, UF e fuso horário IANA.</summary>
public sealed class City : Entity
{
    private City(Guid id, string slug, string name, string state, string timeZone)
    {
        Id = id;
        Slug = slug;
        Name = name;
        State = state;
        TimeZone = timeZone;
    }

    public Guid Id { get; private set; }

    /// <summary>Identificador na URL, <c>^[a-z0-9-]{2,60}$</c> (ex.: <c>cidade-exemplo</c>).</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    /// <summary>UF com 2 letras (ex.: <c>SP</c>).</summary>
    public string State { get; private set; }

    /// <summary>Fuso horário IANA (ex.: <c>America/Sao_Paulo</c>).</summary>
    public string TimeZone { get; private set; }

    public static City Create(string slug, string name, string state, string timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        return new City(Guid.NewGuid(), slug, name, state, timeZone);
    }

    /// <summary>SPEC-0017 — troca nome, UF e fuso; o slug não muda (links compartilhados não quebram).</summary>
    public void Update(string name, string state, string timeZone) =>
        throw new NotImplementedException("SPEC-0017: City.Update ainda não implementado.");
}
