using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Models.KtBsData;

namespace wh40kAPI.Server.Data;

public class KtBsDataDbContext(DbContextOptions<KtBsDataDbContext> options) : DbContext(options)
{
    public DbSet<KtBsDataCatalogue> Catalogues => Set<KtBsDataCatalogue>();
    public DbSet<KtBsDataUnit> Units => Set<KtBsDataUnit>();
    public DbSet<KtBsDataProfile> Profiles => Set<KtBsDataProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite key: a profile is unique per unit + profile id + type name
        modelBuilder.Entity<KtBsDataProfile>()
            .HasKey(p => new { p.UnitId, p.Id, p.TypeName });

        // Composite key: a unit is unique within a catalogue
        modelBuilder.Entity<KtBsDataUnit>()
            .HasKey(u => new { u.CatalogueId, u.Id });

        base.OnModelCreating(modelBuilder);
    }
}
