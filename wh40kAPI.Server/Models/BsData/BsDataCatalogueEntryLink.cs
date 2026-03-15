namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Represents a top-level (catalogue-level) entry link — an entry link declared
/// directly in a catalogue's root <c>&lt;entryLinks&gt;</c> element rather than
/// inside a <c>selectionEntry</c>.  Used by <c>GetDetachments</c> to resolve
/// detachment roots for factions whose Detachment entry lives in a shared
/// library catalogue, and by <c>GetUnitsTree</c> to apply detachment-dependency
/// markers to units that are only available when a specific detachment is selected.
/// </summary>
public class BsDataCatalogueEntryLink
{
    /// <summary>Auto-generated integer primary key.</summary>
    public int Id { get; set; }
    /// <summary>ID of the catalogue that declares this entry link.</summary>
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>ID of the shared entry targeted by this link.</summary>
    public string TargetId { get; set; } = string.Empty;
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
