namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Slim DTO used by the <c>/api/bsdata/fractions/{id}/units-classification</c> endpoint.
/// Contains only <c>id</c>, <c>catalogueId</c>, and <c>categories</c>.
/// </summary>
public class BsDataUnitClassification
{
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>Category projections — only <c>name</c> and <c>primary</c>.</summary>
    public ICollection<BsDataUnitCategorySlim> Categories { get; set; } = [];

    public static BsDataUnitClassification FromUnit(BsDataUnit unit) => new()
    {
        Id = unit.Id,
        CatalogueId = unit.CatalogueId,
        Categories = unit.Categories.Select(c => new BsDataUnitCategorySlim { Name = c.Name, Primary = c.Primary }).ToList(),
    };
}
