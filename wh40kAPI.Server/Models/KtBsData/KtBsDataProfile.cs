using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.KtBsData;

public class KtBsDataProfile
{
    // Id is no longer a standalone primary key — composite key is defined in the DbContext
    public string Id { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // TypeName participates in the composite key, make it non-nullable with a default
    public string TypeName { get; set; } = string.Empty;
    /// <summary>JSON object of characteristic name → value pairs.</summary>
    public string? Characteristics { get; set; }
}
