using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.KtBsData;

public class KtBsDataUnit
{
    // Id is part of a composite key with CatalogueId (configured in DbContext)
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    public string? Points { get; set; }
    /// <summary>Minimum number of this operative that can be selected.</summary>
    public int? MinCount { get; set; }
    /// <summary>Maximum number of this operative that can be selected.</summary>
    public int? MaxCount { get; set; }
    /// <summary>JSON array of category targetIds (archetypes) this operative belongs to.</summary>
    public string? Categories { get; set; }
}
