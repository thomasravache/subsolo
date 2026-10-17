using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Infrastructure.Users;

/// <summary>SPEC-0014 — tabela <c>users.users</c>, com o e-mail normalizado único e os papéis em <c>text[]</c>.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(User.EmailMaxLength);
        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(User.DisplayNameMaxLength);
        builder.PrimitiveCollection(user => user.Roles).HasColumnName("roles");
        builder.Property(user => user.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(user => user.Email).IsUnique();
    }
}
