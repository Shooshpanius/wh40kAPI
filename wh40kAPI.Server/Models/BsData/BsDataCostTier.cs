using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataCostTier
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string UnitId { get; set; } = string.Empty;
    /// <summary>Minimum number of models in this cost tier.</summary>
    public int? MinModels { get; set; }
    /// <summary>Maximum number of models in this cost tier (null if unbounded).</summary>
    public int? MaxModels { get; set; }
    /// <summary>Points cost for this tier.</summary>
    public decimal Points { get; set; }
}
