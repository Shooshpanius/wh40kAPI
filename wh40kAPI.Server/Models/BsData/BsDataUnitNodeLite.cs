namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight DTO used by the <c>/api/bsdata/fractions/{id}/unitsList</c> endpoint.
/// Contains only the fields required for displaying a faction's unit list:
/// <c>id</c>, <c>catalogueId</c>, <c>name</c>, <c>entryType</c>, <c>points</c>,
/// <c>hidden</c>, and <c>categories</c>.
/// </summary>
public class BsDataUnitNodeLite
{
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    /// <summary>Points cost — stored as <see langword="double"/> to avoid 28-digit decimal serialization.</summary>
    public double? Points { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Slim category projections — only <c>name</c> and <c>primary</c>.</summary>
    public ICollection<BsDataUnitCategorySlim> Categories { get; set; } = [];

    public static BsDataUnitNodeLite FromUnit(BsDataUnit unit) => new()
    {
        Id = unit.Id,
        CatalogueId = unit.CatalogueId,
        Name = unit.Name,
        EntryType = unit.EntryType,
        Points = unit.Points is { } p ? (double)p : null,
        Hidden = unit.Hidden,
        Categories = unit.Categories.Select(c => new BsDataUnitCategorySlim { Name = c.Name, Primary = c.Primary }).ToList(),
    };
}
