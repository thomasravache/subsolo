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

    /// <summary>
    /// SPEC-0033 — quando a moderação ocultou o show (<see langword="null"/> = visível). Oculto, o show sai de toda leitura
    /// pública e não pode mais ser editado nem mudar de situação.
    /// </summary>
    public DateTimeOffset? HiddenAtUtc { get; private set; }

    /// <summary>SPEC-0033 — o show foi ocultado pela moderação.</summary>
    public bool IsHidden => HiddenAtUtc is not null;

    /// <summary>
    /// Os perfis envolvidos no show: o organizador, a casa (se tiver perfil) e as bandas do line-up com perfil, sem
    /// repetir.
    /// </summary>
    public IReadOnlyList<Guid> ProfileIds =>
    [
        .. new[] { OrganizerProfileId }
            .Concat(Venue.ProfileId is { } venue ? [venue] : [])
            .Concat(Lineup.Where(item => item.ProfileId is not null).Select(item => item.ProfileId!.Value))
            .Distinct(),
    ];

    /// <summary>
    /// Show novo, <see cref="ShowStatuses.Scheduled"/>, cadastrado por <paramref name="createdBy"/>; levanta
    /// <see cref="ShowPublishedDomainEvent"/>. Quem chama já validou os dados (tamanhos, cidade, estilos e o flyer).
    /// </summary>
    public static Show Create(string slug, ShowData data, Guid createdBy, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(data);
        var show = new Show
        {
            Id = Guid.CreateVersion7(),
            Slug = slug,
            CreatedBy = createdBy,
            CreatedAtUtc = nowUtc,
        };
        show.Apply(data, nowUtc);
        show.Raise(new ShowPublishedDomainEvent(show.Id, show.City, show.ProfileIds, show.StartsAtUtc));
        return show;
    }

    /// <summary>Troca os dados do show (o slug não muda) e levanta <see cref="ShowChangedDomainEvent"/>.</summary>
    public void Update(ShowData data, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(data);
        Apply(data, nowUtc);
        Raise(new ShowChangedDomainEvent(Id, Status, StartsAtUtc));
    }

    /// <summary>
    /// Muda a situação (<see cref="ShowStatuses"/>) e levanta <see cref="ShowChangedDomainEvent"/>; a mesma situação de
    /// antes não muda nada.
    /// </summary>
    public void ChangeStatus(string status, DateTimeOffset nowUtc)
    {
        if (!ShowStatuses.All.Contains(status, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Situação de show desconhecida: {status}.", nameof(status));
        }

        if (status == Status)
        {
            return;
        }

        Status = status;
        UpdatedAtUtc = nowUtc;
        Raise(new ShowChangedDomainEvent(Id, Status, StartsAtUtc));
    }

    /// <summary>
    /// SPEC-0033 — a moderação ocultou o show em <paramref name="hiddenAtUtc"/>. Idempotente: ocultar de novo mantém o
    /// primeiro instante e devolve <see langword="false"/>.
    /// </summary>
    public bool HideByModeration(DateTimeOffset hiddenAtUtc)
    {
        if (IsHidden)
        {
            return false;
        }

        HiddenAtUtc = hiddenAtUtc.ToUniversalTime();
        return true;
    }

    private void Apply(ShowData data, DateTimeOffset nowUtc)
    {
        City = data.City;
        Title = data.Title;
        StartsAtUtc = data.StartsAtUtc.ToUniversalTime();
        OrganizerProfileId = data.OrganizerProfileId;
        Venue = data.Venue;
        Lineup = [.. data.Lineup];
        Styles = [.. data.Styles];
        Price = data.Price;
        TicketUrl = data.TicketUrl;
        FlyerId = data.FlyerId;
        UpdatedAtUtc = nowUtc;
    }
}

/// <summary>SPEC-0023 — os dados editáveis de um show (tudo menos o slug, a situação e quem cadastrou).</summary>
public sealed record ShowData(
    Guid OrganizerProfileId,
    string Title,
    string City,
    DateTimeOffset StartsAtUtc,
    ShowVenue Venue,
    IReadOnlyList<ShowLineupItem> Lineup,
    IReadOnlyList<string> Styles,
    string Price,
    string? TicketUrl,
    string FlyerId);
