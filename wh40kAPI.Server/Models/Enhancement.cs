using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class Enhancement
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string? FactionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cost { get; set; }
    public string? Detachment { get; set; }
    public string? DetachmentId { get; set; }
    public string? Legend { get; set; }
    public string? Description { get; set; }
}
