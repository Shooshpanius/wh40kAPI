using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Data;

public class BsDataDbContext(DbContextOptions<BsDataDbContext> options) : DbContext(options)
{
    public DbSet<BsDataCatalogue> Catalogues => Set<BsDataCatalogue>();
    public DbSet<BsDataUnit> Units => Set<BsDataUnit>();
    public DbSet<BsDataProfile> Profiles => Set<BsDataProfile>();
    public DbSet<BsDataUnitCategory> UnitCategories => Set<BsDataUnitCategory>();
    public DbSet<BsDataInfoLink> InfoLinks => Set<BsDataInfoLink>();
    public DbSet<BsDataEntryLink> EntryLinks => Set<BsDataEntryLink>();
    public DbSet<BsDataConstraint> Constraints => Set<BsDataConstraint>();
    public DbSet<BsDataModifierGroup> ModifierGroups => Set<BsDataModifierGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BsDataUnitCategory>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.Categories)
            .HasForeignKey(c => c.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataProfile>()
            .HasOne<BsDataUnit>()
            .WithMany()
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
            .HasOne<BsDataUnit>()
            .WithMany(u => u.Constraints)
            .HasForeignKey(c => c.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BsDataModifierGroup>()
            .HasOne<BsDataUnit>()
            .WithMany(u => u.ModifierGroups)
            .HasForeignKey(g => g.UnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
