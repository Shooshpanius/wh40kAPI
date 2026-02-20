using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class Faction
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Link { get; set; }
}
