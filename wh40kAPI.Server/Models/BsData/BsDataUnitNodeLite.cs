using System.Text.Json.Serialization;

namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight tree-node wrapper used by the
/// <c>/api/bsdata/fractions/{id}/unitsList</c> endpoint.
/// Compared to <see cref="BsDataUnitNode"/>, this class:
/// <list type="bullet">
///   <item>Replaces <c>InfoLinks</c> with <see cref="BsDataInfoLinkSlim"/> projections
///         (only <c>type</c> and <c>name</c> — <c>id</c> and <c>targetId</c> are omitted).</item>
///   <item>Replaces <c>ModifierGroups</c> with <see cref="BsDataModifierGroupSlim"/> projections
///         (only <c>modifiers</c> and <c>conditions</c> — DB keys are omitted).</item>
///   <item>Replaces <c>Categories</c> with <see cref="BsDataUnitCategorySlim"/> projections
///         (only <c>name</c> and <c>primary</c> — <c>id</c> and <c>unitId</c> are omitted).</item>
///   <item>Replaces <c>CostTiers</c> with <see cref="BsDataCostTierSlim"/> projections
///         (only <c>minModels</c>, <c>maxModels</c>, <c>points</c> — <c>id</c> and <c>unitId</c> are omitted;
///         <c>points</c> is <see langword="double"/> to avoid 28-digit decimal serialization).</item>
///   <item>Omits <c>EntryLinks</c> and <c>Profiles</c> entirely (not needed by the client).</item>
///   <item>For child nodes (depth≥1) both <c>InfoLinks</c> and <c>Categories</c> are
///         empty collections, <c>CatalogueId</c> is an empty string, and <c>ParentId</c>
///         is excluded from serialization — those fields are not needed client-side.</item>
/// </list>
/// </summary>
public class BsDataUnitNodeLite
{
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>Parent entry id — used only for server-side tree building; excluded from JSON output.</summary>
    [JsonIgnore]
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    /// <summary>Points cost — stored as <see langword="double"/> to avoid 28-digit decimal serialization.</summary>
    public double? Points { get; set; }
    public bool Hidden { get; set; }
    public int? MinInRoster { get; set; }
    public int? MaxInRoster { get; set; }
    /// <summary>
    /// Slim category projections — only <c>name</c> and <c>primary</c>.
    /// Populated only for root nodes (depth=0); empty for child nodes.
    /// </summary>
    public ICollection<BsDataUnitCategorySlim> Categories { get; set; } = [];
    /// <summary>
    /// Slim info-link projections — only <c>type</c> and <c>name</c>.
    /// Populated only for root nodes (depth=0); empty for child nodes.
    /// </summary>
    public ICollection<BsDataInfoLinkSlim> InfoLinks { get; set; } = [];
    /// <summary>Slim cost-tier projections — only <c>minModels</c>, <c>maxModels</c>, and <c>points</c>.</summary>
    public ICollection<BsDataCostTierSlim> CostTiers { get; set; } = [];
    /// <summary>
    /// Slim modifier-group projections — only <c>modifiers</c> and <c>conditions</c>.
    /// DB keys (<c>id</c>, <c>unitId</c>) are omitted to reduce payload size.
    /// </summary>
    public ICollection<BsDataModifierGroupSlim> ModifierGroups { get; set; } = [];
    /// <summary>Direct children of this entry in the selection-entry hierarchy.</summary>
    public ICollection<BsDataUnitNodeLite> Children { get; set; } = [];
    /// <summary>
    /// Upgrade children that have a roster-level minimum constraint and are gated behind a
    /// specific detachment.  <c>null</c> when no such children exist.
    /// </summary>
    public ICollection<BsDataRequiredUpgrade>? RequiredUpgrades { get; set; }

    public static BsDataUnitNodeLite FromUnit(BsDataUnit unit) => new()
    {
        Id = unit.Id,
        CatalogueId = unit.CatalogueId,
        ParentId = unit.ParentId,
        Name = unit.Name,
        EntryType = unit.EntryType,
        Points = unit.Points is { } p ? (double)p : null,
        Hidden = unit.Hidden,
        MinInRoster = unit.MinInRoster,
        MaxInRoster = unit.MaxInRoster,
        Categories = unit.Categories.Select(c => new BsDataUnitCategorySlim { Name = c.Name, Primary = c.Primary }).ToList(),
        InfoLinks = unit.InfoLinks.Select(l => new BsDataInfoLinkSlim { Type = l.Type, Name = l.Name }).ToList(),
        CostTiers = unit.CostTiers.Select(t => new BsDataCostTierSlim { MinModels = t.MinModels, MaxModels = t.MaxModels, Points = (double)t.Points }).ToList(),
        ModifierGroups = unit.ModifierGroups.Select(g => new BsDataModifierGroupSlim { Modifiers = g.Modifiers, Conditions = g.Conditions }).ToList(),
    };

    /// <summary>
    /// Creates a copy of <paramref name="source"/> with <see cref="Hidden"/> forced to
    /// <see langword="true"/> and an extra <see cref="BsDataModifierGroupSlim"/> that encodes
    /// the detachment-unlock condition.
    /// </summary>
    public static BsDataUnitNodeLite WithDetachmentDependency(BsDataUnitNodeLite source, string modifiers, string conditions) => new()
    {
        Id = source.Id,
        CatalogueId = source.CatalogueId,
        ParentId = source.ParentId,
        Name = source.Name,
        EntryType = source.EntryType,
        Points = source.Points,
        Hidden = true,
        MinInRoster = source.MinInRoster,
        MaxInRoster = source.MaxInRoster,
        Categories = source.Categories,
        InfoLinks = source.InfoLinks,
        CostTiers = source.CostTiers,
        Children = source.Children,
        ModifierGroups = [.. source.ModifierGroups, new BsDataModifierGroupSlim { Modifiers = modifiers, Conditions = conditions }],
        RequiredUpgrades = source.RequiredUpgrades,
    };
}
