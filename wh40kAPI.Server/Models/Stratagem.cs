using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class Stratagem
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string? FactionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CpCost { get; set; }
    public string? Legend { get; set; }
    public string? Turn { get; set; }
    public string? Phase { get; set; }
    public string? Detachment { get; set; }
    public string? DetachmentId { get; set; }
    public string? Description { get; set; }
}
