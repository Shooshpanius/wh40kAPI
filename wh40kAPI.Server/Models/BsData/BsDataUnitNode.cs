namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// A tree-node wrapper around <see cref="BsDataUnit"/> used by the
/// <c>/api/bsdata/fractions/{id}/unitsTree</c> endpoint.
/// Root nodes have <see cref="ParentId"/> == <see langword="null"/>;
/// nested entries are placed in <see cref="Children"/>.
/// </summary>
public class BsDataUnitNode
{
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>Parent entry id — null for top-level nodes.</summary>
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    public decimal? Points { get; set; }
    public bool Hidden { get; set; }
    public bool Collective { get; set; }
    public bool Import { get; set; }
    public string? PublicationId { get; set; }
    public string? Page { get; set; }
    public int? MinInRoster { get; set; }
    public int? MaxInRoster { get; set; }
    public ICollection<BsDataUnitCategory> Categories { get; set; } = [];
    public ICollection<BsDataInfoLink> InfoLinks { get; set; } = [];
    public ICollection<BsDataEntryLink> EntryLinks { get; set; } = [];
    public ICollection<BsDataCostTier> CostTiers { get; set; } = [];
    public ICollection<BsDataModifierGroup> ModifierGroups { get; set; } = [];
    /// <summary>Direct children of this entry in the selection-entry hierarchy.</summary>
    public ICollection<BsDataUnitNode> Children { get; set; } = [];

    public static BsDataUnitNode FromUnit(BsDataUnit unit) => new()
    {
        Id = unit.Id,
        CatalogueId = unit.CatalogueId,
        ParentId = unit.ParentId,
        Name = unit.Name,
        EntryType = unit.EntryType,
        Points = unit.Points,
        Hidden = unit.Hidden,
        Collective = unit.Collective,
        Import = unit.Import,
        PublicationId = unit.PublicationId,
        Page = unit.Page,
        MinInRoster = unit.MinInRoster,
        MaxInRoster = unit.MaxInRoster,
        Categories = unit.Categories,
        InfoLinks = unit.InfoLinks,
        EntryLinks = unit.EntryLinks,
        CostTiers = unit.CostTiers,
        ModifierGroups = unit.ModifierGroups,
    };
}
