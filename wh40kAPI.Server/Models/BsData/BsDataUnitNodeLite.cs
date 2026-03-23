namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight DTO used by the <c>/api/bsdata/fractions/{id}/unitsList</c> endpoint.
/// Contains only the fields required for displaying a faction's unit list:
/// <c>id</c>, <c>catalogueId</c>, <c>name</c>, <c>entryType</c>, <c>points</c>,
/// <c>hidden</c>, <c>categories</c>, and <c>requiredUpgrades</c>.
/// </summary>
public class BsDataUnitNodeLite
{
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    /// <summary>Points cost — stored as <see langword="decimal"/> for exact representation.</summary>
    public decimal? Points { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Slim category projections — only <c>name</c> and <c>primary</c>.</summary>
    public ICollection<BsDataUnitCategorySlim> Categories { get; set; } = [];
    /// <summary>
    /// Upgrade children that have a roster-level minimum constraint and are gated behind a
    /// specific detachment.  Populated only for root nodes with <c>entryType="model"</c> whose
    /// upgrade sub-entries carry <c>minInRoster &gt; 0</c> and a detachment-hide condition,
    /// so that API consumers can surface the requirement without a separate tree request.
    /// <c>null</c> when no such children exist.
    /// </summary>
    public ICollection<BsDataRequiredUpgrade>? RequiredUpgrades { get; set; }

    public static BsDataUnitNodeLite FromUnit(BsDataUnit unit) => new()
    {
        Id = unit.Id,
        CatalogueId = unit.CatalogueId,
        Name = unit.Name,
        EntryType = unit.EntryType,
        Points = unit.Points is { } p ? Math.Round(p, 2) : null,  // Round defensively to handle legacy DB values imported before this fix
        Hidden = unit.Hidden,
        Categories = unit.Categories.Select(c => new BsDataUnitCategorySlim { Name = c.Name, Primary = c.Primary }).ToList(),
    };
}
