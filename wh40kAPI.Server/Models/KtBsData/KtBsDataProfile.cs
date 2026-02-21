using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.KtBsData;

public class KtBsDataProfile
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    /// <summary>JSON object of characteristic name → value pairs.</summary>
    public string? Characteristics { get; set; }
}
