using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataInfoLink
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string? Type { get; set; }
}
