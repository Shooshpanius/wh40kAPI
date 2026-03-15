using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Stores a hidden-modifier condition parsed from a direct &lt;modifiers&gt; element
/// on a detachment selection entry.  Used to filter detachment entries by faction.
/// </summary>
/// <remarks>
/// In BSData catalogues every detachment entry that is not available to all factions
/// carries a &lt;modifier type="set" field="hidden" value="true"&gt; with a single
/// &lt;condition scope="primary-catalogue"&gt; child.  The two patterns used are:
/// <list type="bullet">
///   <item><c>notInstanceOf</c> — the entry is hidden when the primary catalogue is
///   <em>not</em> the specified one, i.e. the detachment is only for that catalogue.</item>
///   <item><c>instanceOf</c> — the entry is hidden when the primary catalogue <em>is</em>
///   the specified one, i.e. the detachment is excluded from that catalogue.</item>
/// </list>
/// </remarks>
public class BsDataDetachmentVisibility
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>The BSData id of the detachment selection entry this condition belongs to.</summary>
    public string UnitId { get; set; } = string.Empty;

    /// <summary>"notInstanceOf" or "instanceOf".</summary>
    public string ConditionType { get; set; } = string.Empty;

    /// <summary>The catalogue id referenced in the condition's <c>childId</c> attribute.</summary>
    public string CatalogueId { get; set; } = string.Empty;
}
