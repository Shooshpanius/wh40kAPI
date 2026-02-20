using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class Ability
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Legend { get; set; }
    public string? FactionId { get; set; }
    public string? Description { get; set; }
}
