namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Represents a top-level (catalogue-level) entry link — an entry link declared
/// directly in a catalogue's root <c>&lt;entryLinks&gt;</c> element rather than
/// inside a <c>selectionEntry</c>.  Used by <c>GetDetachments</c> to resolve
/// detachment roots for factions whose Detachment entry lives in a shared
/// library catalogue.
/// </summary>
public class BsDataCatalogueEntryLink
{
    /// <summary>Auto-generated integer primary key.</summary>
    public int Id { get; set; }
    /// <summary>ID of the catalogue that declares this entry link.</summary>
    public string CatalogueId { get; set; } = string.Empty;
    /// <summary>ID of the shared entry targeted by this link.</summary>
    public string TargetId { get; set; } = string.Empty;
}
