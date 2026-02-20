using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Ability> Abilities => Set<Ability>();
    public DbSet<Datasheet> Datasheets => Set<Datasheet>();
    public DbSet<DatasheetAbility> DatasheetAbilities => Set<DatasheetAbility>();
    public DbSet<DatasheetDetachmentAbility> DatasheetDetachmentAbilities => Set<DatasheetDetachmentAbility>();
    public DbSet<DatasheetEnhancement> DatasheetEnhancements => Set<DatasheetEnhancement>();
    public DbSet<DatasheetKeyword> DatasheetKeywords => Set<DatasheetKeyword>();
    public DbSet<DatasheetLeader> DatasheetLeaders => Set<DatasheetLeader>();
    public DbSet<DatasheetModel> DatasheetModels => Set<DatasheetModel>();
    public DbSet<DatasheetModelCost> DatasheetModelCosts => Set<DatasheetModelCost>();
    public DbSet<DatasheetOption> DatasheetOptions => Set<DatasheetOption>();
    public DbSet<DatasheetStratagem> DatasheetStratagems => Set<DatasheetStratagem>();
    public DbSet<DatasheetUnitComposition> DatasheetUnitCompositions => Set<DatasheetUnitComposition>();
    public DbSet<DatasheetWargear> DatasheetWargears => Set<DatasheetWargear>();
    public DbSet<DetachmentAbility> DetachmentAbilities => Set<DetachmentAbility>();
    public DbSet<Detachment> Detachments => Set<Detachment>();
    public DbSet<Enhancement> Enhancements => Set<Enhancement>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<LastUpdate> LastUpdates => Set<LastUpdate>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Stratagem> Stratagems => Set<Stratagem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ability: composite key (id + faction_id, since same ability can appear for multiple factions)
        modelBuilder.Entity<Ability>()
            .HasKey(a => new { a.Id, a.FactionId });

        // DatasheetAbility: composite key
        modelBuilder.Entity<DatasheetAbility>()
            .HasKey(da => new { da.DatasheetId, da.Line });

        // DatasheetDetachmentAbility: composite key
        modelBuilder.Entity<DatasheetDetachmentAbility>()
            .HasKey(dda => new { dda.DatasheetId, dda.DetachmentAbilityId });

        // DatasheetEnhancement: composite key
        modelBuilder.Entity<DatasheetEnhancement>()
            .HasKey(de => new { de.DatasheetId, de.EnhancementId });

        // DatasheetKeyword: composite key
        modelBuilder.Entity<DatasheetKeyword>()
            .HasKey(dk => new { dk.DatasheetId, dk.Keyword, dk.Model });

        // DatasheetLeader: composite key
        modelBuilder.Entity<DatasheetLeader>()
            .HasKey(dl => new { dl.LeaderId, dl.AttachedId });

        // DatasheetModel: composite key
        modelBuilder.Entity<DatasheetModel>()
            .HasKey(dm => new { dm.DatasheetId, dm.Line });

        // DatasheetModelCost: composite key
        modelBuilder.Entity<DatasheetModelCost>()
            .HasKey(dmc => new { dmc.DatasheetId, dmc.Line });

        // DatasheetOption: composite key
        modelBuilder.Entity<DatasheetOption>()
            .HasKey(dopt => new { dopt.DatasheetId, dopt.Line });

        // DatasheetStratagem: composite key
        modelBuilder.Entity<DatasheetStratagem>()
            .HasKey(ds => new { ds.DatasheetId, ds.StratagemId });

        // DatasheetUnitComposition: composite key
        modelBuilder.Entity<DatasheetUnitComposition>()
            .HasKey(duc => new { duc.DatasheetId, duc.Line });

        // DatasheetWargear: composite key
        modelBuilder.Entity<DatasheetWargear>()
            .HasKey(dw => new { dw.DatasheetId, dw.Line, dw.LineInWargear });

        // DetachmentAbility: id is unique, no composite key needed
    }
}
