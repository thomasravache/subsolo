namespace Subsolo.Modules.Profiles.Domain.Requests;

/// <summary>SPEC-0021 — situação de um pedido de perfil.</summary>
public static class ProfileRequestStatuses
{
    public const string Pending = "pending";

    public const string Approved = "approved";

    public const string Rejected = "rejected";
}
