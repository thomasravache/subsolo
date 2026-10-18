using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Infrastructure.Requests;

/// <summary>
/// SPEC-0021 — tabela <c>profiles.profile_requests</c>: tipo do pedido, quem pediu, o perfil (reivindicado ou criado na
/// aprovação), os dados do perfil pedido em <c>payload</c> (<c>jsonb</c>), o contato ou a prova, a situação, o motivo
/// da recusa e as datas; índices de quem pediu (e o único parcial dos pendentes, Emenda 3), da fila (situação + criação) e da limpeza (decisão).
/// </summary>
internal sealed class ProfileRequestConfiguration : IEntityTypeConfiguration<ProfileRequest>
{
    public const int KindMaxLength = 10;

    public const int StatusMaxLength = 10;

    public void Configure(EntityTypeBuilder<ProfileRequest> builder)
    {
        builder.ToTable("profile_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").ValueGeneratedNever();
        // Emenda 3: token de concorrência otimista (a coluna de sistema xmin do PostgreSQL).
        builder.Property<uint>(ProfilesDbContextVersion.Name).IsRowVersion();
        builder.Property(request => request.Kind).HasColumnName("kind").HasMaxLength(KindMaxLength);
        builder.Property(request => request.RequesterUserId).HasColumnName("requester_user_id");
        builder.Property(request => request.ProfileId).HasColumnName("profile_id");
        builder.Property(request => request.Draft)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<ProfileDraft?>(), JsonColumn.Comparer<ProfileDraft?>());
        builder.Property(request => request.ContactOrProof)
            .HasColumnName("contact_or_proof")
            .HasMaxLength(ProfileRequest.ProofMaxLength);
        builder.Property(request => request.Status).HasColumnName("status").HasMaxLength(StatusMaxLength);
        builder.Property(request => request.Reason).HasColumnName("reason").HasMaxLength(ProfileRequest.ContactMaxLength);
        builder.Property(request => request.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(request => request.DecidedAtUtc).HasColumnName("decided_at_utc");
        builder.Ignore(request => request.IsPending);
        builder.HasIndex(request => request.RequesterUserId);
        builder.HasIndex(request => request.RequesterUserId, ProfilesDbContext.PendingRequestIndex)
            .IsUnique()
            .HasFilter($"status = '{ProfileRequestStatuses.Pending}'");
        builder.HasIndex(request => new { request.Status, request.CreatedAtUtc });
        builder.HasIndex(request => request.DecidedAtUtc);
    }
}
