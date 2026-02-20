using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models;

public class LastUpdate
{
    [Key]
    public int Id { get; set; }
    public string? UpdatedAt { get; set; }
}
