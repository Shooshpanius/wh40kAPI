using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataUnitCategory
{
    [Key]
    public int Id { get; set; }
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Primary { get; set; }
}
