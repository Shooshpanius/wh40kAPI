using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Data;

public class BsDataDbContext(DbContextOptions<BsDataDbContext> options) : DbContext(options)
{
    public DbSet<BsDataCatalogue> Catalogues => Set<BsDataCatalogue>();
    public DbSet<BsDataCatalogueLink> CatalogueLinks => Set<BsDataCatalogueLink>();
    public DbSet<BsDataCatalogueEntryLink> CatalogueLevelEntryLinks => Set<BsDataCatalogueEntryLink>();
    public DbSet<BsDataRule> Rules => Set<BsDataRule>();
    public DbSet<BsDataUnit> Units => Set<BsDataUnit>();
    public DbSet<BsDataProfile> Profiles => Set<BsDataProfile>();
    public DbSet<BsDataUnitCategory> UnitCategories => Set<BsDataUnitCategory>();
    public DbSet<BsDataInfoLink> InfoLinks => Set<BsDataInfoLink>();
    public DbSet<BsDataEntryLink> EntryLinks => Set<BsDataEntryLink>();
    public DbSet<BsDataConstraint> Constraints => Set<BsDataConstraint>();
    public DbSet<BsDataModifierGroup> ModifierGroups => Set<BsDataModifierGroup>();
    public DbSet<BsDataDetachmentVisibility> DetachmentVisibilities => Set<BsDataDetachmentVisibility>();
    public DbSet<BsDataCostTier> CostTiers => Set<BsDataCostTier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BsDataCatalogueEntryLink>()
            .HasOne<BsDataCatalogue>()
            .WithMany()
            .HasForeignKey(l => l.CatalogueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataCatalogueLink>()
            .HasOne<BsDataCatalogue>()
            .WithMany()
            .HasForeignKey(l => l.CatalogueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataRule>()
            .HasOne<BsDataCatalogue>()
            .WithMany()
            .HasForeignKey(r => r.CatalogueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataUnitCategory>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.Categories)
            .HasForeignKey(c => c.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataProfile>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.Profiles)
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataInfoLink>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.InfoLinks)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataEntryLink>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.EntryLinks)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataConstraint>()
            .HasKey(c => new { c.UnitId, c.Id });

        modelBuilder.Entity<BsDataConstraint>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.Constraints)
            .HasForeignKey(c => c.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataModifierGroup>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.ModifierGroups)
            .HasForeignKey(g => g.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataCostTier>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.CostTiers)
            .HasForeignKey(t => t.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataDetachmentVisibility>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.DetachmentVisibilities)
            .HasForeignKey(v => v.UnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
