using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrandWorkspace.Persistence.Access;

/// <summary>Identity mapping; schema is applied only by the embedded SQL migration owner.</summary>
public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : IdentityDbContext<AccessUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("access");
        builder.Entity<AccessUser>(user =>
        {
            user.ToTable("users");
            user.Property(value => value.Id).HasColumnName("id");
            user.Property(value => value.DisplayName).HasMaxLength(120);
            user.HasIndex(value => value.NormalizedEmail).IsUnique();
        });
        builder.Entity<IdentityRole>().ToTable("identity_roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("identity_user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("identity_user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("identity_user_logins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("identity_role_claims");
        builder.Entity<IdentityUserToken<string>>().ToTable("identity_user_tokens");
    }
}
