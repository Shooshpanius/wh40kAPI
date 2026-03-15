using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataUnit
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>Parent entry id for nested selection entries (null for top-level).</summary>
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    public decimal? Points { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Whether this entry is collective (its children share one selection).</summary>
    public bool Collective { get; set; }
    /// <summary>Whether this entry is imported from another catalogue.</summary>
    public bool Import { get; set; }
    public string? PublicationId { get; set; }
    public string? Page { get; set; }
    public int? MinInRoster { get; set; }
    public int? MaxInRoster { get; set; }
    public ICollection<BsDataUnitCategory> Categories { get; set; } = [];
    public ICollection<BsDataInfoLink> InfoLinks { get; set; } = [];
    public ICollection<BsDataEntryLink> EntryLinks { get; set; } = [];
    public ICollection<BsDataConstraint> Constraints { get; set; } = [];
    public ICollection<BsDataModifierGroup> ModifierGroups { get; set; } = [];
    public ICollection<BsDataCostTier> CostTiers { get; set; } = [];
    public ICollection<BsDataDetachmentVisibility> DetachmentVisibilities { get; set; } = [];
}
