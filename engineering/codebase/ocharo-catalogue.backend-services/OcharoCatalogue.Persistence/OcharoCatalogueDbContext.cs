using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Persistence;

/// <summary>EF Core context over Catalogue's PostgreSQL schema (<c>catalogue</c>): document rows plus indexed scalars.
/// The <c>catalogue</c> migration set owns the tables; this context only maps them.</summary>
public sealed class OcharoCatalogueDbContext(DbContextOptions<OcharoCatalogueDbContext> options) : DbContext(options)
{
    public DbSet<ProductRecord> Products => Set<ProductRecord>();
    public DbSet<ProductRevisionRecord> ProductRevisions => Set<ProductRevisionRecord>();
    public DbSet<CollectionRecord> Collections => Set<CollectionRecord>();
    public DbSet<MediaBlobRecord> MediaBlobs => Set<MediaBlobRecord>();
    public DbSet<SettingsRecord> Settings => Set<SettingsRecord>();

    public DbSet<CatalogueDesignRecord> CatalogueDesigns => Set<CatalogueDesignRecord>();
    public DbSet<CatalogueDesignRevisionRecord> CatalogueDesignRevisions => Set<CatalogueDesignRevisionRecord>();
    public DbSet<CatalogueReleaseRecord> CatalogueReleases => Set<CatalogueReleaseRecord>();
    public DbSet<CataloguePublicationRecord> CataloguePublications => Set<CataloguePublicationRecord>();
    public DbSet<CatalogueReleaseAssetRecord> CatalogueReleaseAssets => Set<CatalogueReleaseAssetRecord>();
    public DbSet<CatalogueReleaseFileRecord> CatalogueReleaseFiles => Set<CatalogueReleaseFileRecord>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // `timestamptz` holds an instant at offset zero; a value with another offset is written as the same instant.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcInstantConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(CatalogueDatabaseConstants.Schema);
        modelBuilder.Entity<CatalogueDesignRecord>(e =>
        {
            e.ToTable("catalogue_designs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<CatalogueDesignRevisionRecord>(e =>
        {
            e.ToTable("catalogue_design_revisions");
            e.HasKey(x => x.Id);
            e.HasOne<CatalogueDesignRecord>().WithMany().HasForeignKey(x => x.DesignId);
        });
        modelBuilder.Entity<CatalogueReleaseRecord>(e =>
        {
            e.ToTable("catalogue_releases");
            e.HasKey(x => x.Id);
            e.HasOne<CatalogueDesignRecord>().WithMany().HasForeignKey(x => x.DesignId);
        });
        modelBuilder.Entity<CataloguePublicationRecord>(e =>
        {
            e.ToTable("catalogue_publications");
            e.HasKey(x => x.Key);
            e.HasOne<CatalogueDesignRecord>().WithMany().HasForeignKey(x => x.DesignId);
            e.HasOne<CatalogueReleaseRecord>().WithMany().HasForeignKey(x => x.ReleaseId);
        });
        modelBuilder.Entity<CatalogueReleaseAssetRecord>(e =>
        {
            e.ToTable("catalogue_release_assets");
            e.HasKey(x => x.Sha256);
        });
        modelBuilder.Entity<CatalogueReleaseFileRecord>(e =>
        {
            e.ToTable("catalogue_release_files");
            e.HasKey(x => x.Sha256);
        });
        modelBuilder.Entity<ProductRecord>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<ProductRevisionRecord>(e =>
        {
            e.ToTable("product_revisions");
            e.HasKey(x => x.Id);
            e.HasOne<ProductRecord>().WithMany().HasForeignKey(x => x.ProductId);
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

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ColumnName(property.Name));
        }
    }

    /// <summary>Names a record property's column: snake_case, <c>Json</c> as <c>document</c>, and an instant's
    /// <c>…AtUtc</c> as <c>…_at</c> (<c>timestamptz</c> is UTC by type).</summary>
    internal static string ColumnName(string property)
    {
        if (property == nameof(ProductRecord.Json))
            return "document";
        if (property.EndsWith("AtUtc", StringComparison.Ordinal))
            property = property[..^3];
        var name = new StringBuilder(property.Length + 4);
        for (var index = 0; index < property.Length; index++)
        {
            var character = property[index];
            if (char.IsUpper(character) && index > 0 && !char.IsDigit(property[index - 1]))
                name.Append('_');
            name.Append(char.ToLowerInvariant(character));
        }

        return name.ToString();
    }

    /// <summary>Writes every instant at offset zero, as Npgsql requires for <c>timestamptz</c>.</summary>
    private sealed class UtcInstantConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        value => value.ToUniversalTime(),
        value => value);
}
