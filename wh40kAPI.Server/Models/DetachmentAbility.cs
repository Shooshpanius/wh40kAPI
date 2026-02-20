using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class DetachmentAbility
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string? FactionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Legend { get; set; }
    public string? Description { get; set; }
    public string? Detachment { get; set; }
    public string? DetachmentId { get; set; }
}
