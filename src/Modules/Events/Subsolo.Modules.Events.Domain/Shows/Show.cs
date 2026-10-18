using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Domain.Shows;

/// <summary>
/// SPEC-0023 — show da agenda: perfil organizador, título (2–100), slug (<c>&lt;titulo&gt;-&lt;aaaa-mm-dd&gt;</c>, único
/// por cidade, imutável), cidade (slug), início em UTC, casa (perfil ou nome livre), line-up (1–15), estilos (1–5),
/// preço (até 40), link do ingresso (<c>https</c>, opcional), flyer e situação (<see cref="ShowStatuses"/>). Criar levanta
/// <see cref="ShowPublishedDomainEvent"/>; editar ou mudar a situação, <see cref="ShowChangedDomainEvent"/>.
/// </summary>
public sealed class Show : Entity
{
    /// <summary>Tamanho mínimo do título.</summary>
    public const int TitleMinLength = 2;

    /// <summary>Tamanho máximo do título.</summary>
    public const int TitleMaxLength = 100;

    /// <summary>Tamanho mínimo do nome livre da casa.</summary>
    public const int VenueNameMinLength = 2;

    /// <summary>Tamanho máximo do nome livre da casa e de uma banda do line-up.</summary>
    public const int NameMaxLength = 80;

    /// <summary>Bandas no line-up: mínimo.</summary>
    public const int LineupMin = 1;

    /// <summary>Bandas no line-up: máximo.</summary>
    public const int LineupMax = 15;

    /// <summary>Estilos: mínimo.</summary>
    public const int StylesMin = 1;

    /// <summary>Estilos: máximo.</summary>
    public const int StylesMax = 5;

    /// <summary>Tamanho máximo do preço (texto livre, ex.: "R$ 30 / R$ 40 na hora").</summary>
    public const int PriceMaxLength = 40;

    private Show()
    {
    }

    public Guid Id { get; private set; }

    /// <summary><c>&lt;titulo&gt;-&lt;aaaa-mm-dd&gt;</c> (data local da cidade), único por cidade; não muda na edição.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Slug da cidade.</summary>
    public string City { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>Início em UTC (o cadastro recebe a data e a hora locais da cidade).</summary>
    public DateTimeOffset StartsAtUtc { get; private set; }

    public Guid OrganizerProfileId { get; private set; }

    public ShowVenue Venue { get; private set; } = new(null, string.Empty);

    public IReadOnlyList<ShowLineupItem> Lineup { get; private set; } = [];

    /// <summary>Slugs dos estilos.</summary>
    public IReadOnlyList<string> Styles { get; private set; } = [];

    public string Price { get; private set; } = string.Empty;

    public string? TicketUrl { get; private set; }

    /// <summary>Id do flyer (SPEC-0022).</summary>
    public string FlyerId { get; private set; } = string.Empty;

    /// <summary><see cref="ShowStatuses"/>.</summary>
    public string Status { get; private set; } = ShowStatuses.Scheduled;

    /// <summary>Usuário que cadastrou (o dono do organizador ou o admin).</summary>
    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
