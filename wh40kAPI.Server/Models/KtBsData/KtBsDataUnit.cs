using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.KtBsData;

public class KtBsDataUnit
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    public string? Points { get; set; }
}
