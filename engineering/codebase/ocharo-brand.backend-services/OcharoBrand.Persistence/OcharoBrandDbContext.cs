using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Persistence;

/// <summary>EF Core context for the brand store (SQLite).</summary>
public sealed class OcharoBrandDbContext(DbContextOptions<OcharoBrandDbContext> options) : DbContext(options)
{
    public DbSet<DraftRecord> Drafts => Set<DraftRecord>();
    public DbSet<DraftRevisionRecord> DraftRevisions => Set<DraftRevisionRecord>();
    public DbSet<LogoVersionRecord> LogoVersions => Set<LogoVersionRecord>();
    public DbSet<ReleaseRecord> Releases => Set<ReleaseRecord>();
    public DbSet<AppearanceRecord> Appearances => Set<AppearanceRecord>();
    public DbSet<MediaBlobRecord> MediaBlobs => Set<MediaBlobRecord>();
    public DbSet<ConsumerRecord> Consumers => Set<ConsumerRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DraftRecord>(e => { e.ToTable("drafts"); e.HasKey(x => x.Id); e.Property(x => x.Revision).IsConcurrencyToken(); });
        modelBuilder.Entity<DraftRevisionRecord>(e => { e.ToTable("draft_revisions"); e.HasKey(x => x.Id); e.HasIndex(x => x.Revision).IsUnique(); });
        modelBuilder.Entity<LogoVersionRecord>(e => { e.ToTable("logo_versions"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.Slot, x.Sha256 }).IsUnique(); });
        modelBuilder.Entity<ReleaseRecord>(e => { e.ToTable("releases"); e.HasKey(x => x.Version); e.Property(x => x.Version).ValueGeneratedNever(); });
        modelBuilder.Entity<AppearanceRecord>(e => { e.ToTable("appearances"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.Status, x.StartsAt }); });
        modelBuilder.Entity<MediaBlobRecord>(e => { e.ToTable("media_blobs"); e.HasKey(x => x.Sha256); });
        modelBuilder.Entity<ConsumerRecord>(e => { e.ToTable("consumers"); e.HasKey(x => x.App); });
    }
}
