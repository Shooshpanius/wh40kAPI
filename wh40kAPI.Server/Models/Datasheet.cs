using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class Datasheet
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FactionId { get; set; }
    public string? SourceId { get; set; }
    public string? Legend { get; set; }
    public string? Role { get; set; }
    public string? Loadout { get; set; }
    public string? Transport { get; set; }
    public string? IsVirtual { get; set; }
    public string? LeaderHead { get; set; }
    public string? LeaderFooter { get; set; }
    public string? DamagedW { get; set; }
    public string? DamagedDescription { get; set; }
    public string? Link { get; set; }
}
