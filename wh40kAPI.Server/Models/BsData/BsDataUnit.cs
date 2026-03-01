using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataUnit
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string CatalogueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EntryType { get; set; }
    public string? Points { get; set; }
    public bool Hidden { get; set; }
    public ICollection<BsDataUnitCategory> Categories { get; set; } = [];
}
