using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataCatalogue
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Revision { get; set; }
    public bool Library { get; set; }
    public string? BattleScribeVersion { get; set; }
    public string? GameSystemId { get; set; }
    public int? GameSystemRevision { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorContact { get; set; }
    public string? AuthorUrl { get; set; }
    public DateTime? FetchedAt { get; set; }
}
