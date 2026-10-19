namespace Subsolo.Modules.Engagement.Domain.Follows;

/// <summary>SPEC-0027 — tipos de alvo de um follow (coluna <c>target_kind</c>).</summary>
public static class FollowTargetKinds
{
    /// <summary>Cidade, pela chave = slug.</summary>
    public const string City = "city";

    /// <summary>Perfil (banda, casa ou produtor), pela chave = id (<c>Guid</c> no formato <c>D</c>).</summary>
    public const string Profile = "profile";
}
