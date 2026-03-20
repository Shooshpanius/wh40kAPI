namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight tree-node wrapper used by the
/// <c>/api/bsdata/fractions/{id}/unitsList</c> endpoint.
/// Compared to <see cref="BsDataUnitNode"/>, this class:
/// <list type="bullet">
///   <item>Replaces <c>InfoLinks</c> with <see cref="BsDataInfoLinkSlim"/> projections
///         (only <c>type</c> and <c>name</c> — <c>id</c> and <c>targetId</c> are omitted).</item>
///   <item>Omits <c>EntryLinks</c> and <c>Profiles</c> entirely (not needed by the client).</item>
///   <item>For child nodes (depth≥1) both <c>InfoLinks</c> and <c>Categories</c> are
///         empty collections — those fields are not loaded from the database.</item>
/// </list>
/// </summary>
public class BsDataUnitNodeLite
{
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>Parent entry id — null for top-level nodes.</summary>
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    public decimal? Points { get; set; }
    public bool Hidden { get; set; }
    public int? MinInRoster { get; set; }
    public int? MaxInRoster { get; set; }
    public ICollection<BsDataUnitCategory> Categories { get; set; } = [];
    /// <summary>
    /// Slim info-link projections — only <c>type</c> and <c>name</c>.
    /// Populated only for root nodes (depth=0); empty for child nodes.
    /// </summary>
    public ICollection<BsDataInfoLinkSlim> InfoLinks { get; set; } = [];
    public ICollection<BsDataCostTier> CostTiers { get; set; } = [];
    public ICollection<BsDataModifierGroup> ModifierGroups { get; set; } = [];
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
        Points = unit.Points,
        Hidden = unit.Hidden,
        MinInRoster = unit.MinInRoster,
        MaxInRoster = unit.MaxInRoster,
        Categories = unit.Categories,
        InfoLinks = unit.InfoLinks.Select(l => new BsDataInfoLinkSlim { Type = l.Type, Name = l.Name }).ToList(),
        CostTiers = unit.CostTiers,
        ModifierGroups = unit.ModifierGroups,
    };

    /// <summary>
    /// Creates a copy of <paramref name="source"/> with <see cref="Hidden"/> forced to
    /// <see langword="true"/> and an extra <see cref="BsDataModifierGroup"/> that encodes
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
        ModifierGroups = [.. source.ModifierGroups, new BsDataModifierGroup { UnitId = source.Id, Modifiers = modifiers, Conditions = conditions }],
        RequiredUpgrades = source.RequiredUpgrades,
    };
}
