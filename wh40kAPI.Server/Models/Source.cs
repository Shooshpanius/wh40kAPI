using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class Source
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Edition { get; set; }
    public string? Version { get; set; }
    public string? ErrataDate { get; set; }
    public string? ErrataLink { get; set; }
}
