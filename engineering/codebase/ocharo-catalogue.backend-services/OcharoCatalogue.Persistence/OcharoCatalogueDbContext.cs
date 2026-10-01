using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Persistence;

/// <summary>EF Core context for the catalogue store (SQLite): document rows plus indexed scalars.</summary>
public sealed class OcharoCatalogueDbContext(DbContextOptions<OcharoCatalogueDbContext> options) : DbContext(options)
{
    public DbSet<ProductRecord> Products => Set<ProductRecord>();
    public DbSet<ProductRevisionRecord> ProductRevisions => Set<ProductRevisionRecord>();
    public DbSet<CollectionRecord> Collections => Set<CollectionRecord>();
    public DbSet<MediaBlobRecord> MediaBlobs => Set<MediaBlobRecord>();
    public DbSet<SettingsRecord> Settings => Set<SettingsRecord>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot ORDER BY a TEXT-stored DateTimeOffset — store it as a sortable binary long instead.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductRecord>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Json).IsRequired();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => new { x.Status, x.SortOrder });
            e.HasIndex(x => x.GarmentType);
        });
        modelBuilder.Entity<ProductRevisionRecord>(e =>
        {
            e.ToTable("product_revisions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProductId, x.Revision }).IsUnique();
            e.Property(x => x.Json).IsRequired();
        });
        modelBuilder.Entity<CollectionRecord>(e =>
        {
            e.ToTable("collections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<MediaBlobRecord>(e =>
        {
            e.ToTable("media_blobs");
            e.HasKey(x => x.Sha256);
        });
        modelBuilder.Entity<SettingsRecord>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Revision).IsConcurrencyToken();
        });
    }
}
