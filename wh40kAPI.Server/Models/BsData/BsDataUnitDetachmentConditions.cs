namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// DTO returned by <c>/api/bsdata/fractions/{id}/detachment-conditions</c>.
/// Describes which detachments are required to unlock a given unit in the roster.
/// </summary>
public class BsDataUnitDetachmentConditions
{
    /// <summary>BSData entry id of the unit (matches <c>BsDataUnit.Id</c>).</summary>
    public required string UnitId { get; set; }
    /// <summary>
    /// IDs of the detachments that unlock this unit (the <c>childId</c> values from
    /// the roster-scope conditions stored in <c>CatalogueLevelEntryLinks.DetachmentConditions</c>).
    /// </summary>
    public required IReadOnlyList<string> DetachmentIds { get; set; }
}
