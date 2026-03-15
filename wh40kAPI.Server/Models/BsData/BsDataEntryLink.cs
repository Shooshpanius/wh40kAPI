using System.ComponentModel.DataAnnotations;

namespace wh40kAPI.Server.Models.BsData;

public class BsDataEntryLink
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string? Type { get; set; }
    /// <summary>
    /// JSON array of unlock modifier objects when this link hides its target unless a
    /// specific detachment is selected (e.g. <c>[{"field":"hidden","type":"set","value":"false"}]</c>).
    /// Non-null only when <see cref="DetachmentConditions"/> is also set.
    /// </summary>
    public string? DetachmentModifiers { get; set; }
    /// <summary>
    /// JSON array of roster-scope conditions that unlock the target unit
    /// (e.g. <c>[{"field":"selections","scope":"roster","type":"atLeast","value":"1","childId":"&lt;DETACHMENT-ID&gt;"}]</c>).
    /// Non-null only when the entryLink XML contains a hide-unless-detachment modifier pattern.
    /// </summary>
    public string? DetachmentConditions { get; set; }
}
