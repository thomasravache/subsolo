namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>SPEC-0021 — tipos de perfil (os mesmos valores do contrato HTTP).</summary>
public static class ProfileTypes
{
    public const string Band = "band";

    public const string Venue = "venue";

    public const string Producer = "producer";

    /// <summary>Os três tipos.</summary>
    public static readonly IReadOnlyList<string> All = [Band, Venue, Producer];
}
