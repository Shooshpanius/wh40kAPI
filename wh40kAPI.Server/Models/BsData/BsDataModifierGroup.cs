using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataModifierGroup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string UnitId { get; set; } = string.Empty;
    /// <summary>JSON array of modifier objects (id, field, type, value).</summary>
    public string? Modifiers { get; set; }
    /// <summary>JSON array of condition objects (id, field, scope, value, type, childId).</summary>
    public string? Conditions { get; set; }
}
