using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.KtBsData;

public class KtBsDataCatalogue
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Revision { get; set; }
    public DateTime? FetchedAt { get; set; }
}
