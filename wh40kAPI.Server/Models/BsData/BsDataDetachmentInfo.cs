namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight DTO returned by <c>/api/bsdata/fractions/{id}/detachments</c>.
/// Carries the BSData entry id alongside the human-readable name so that
/// front-end code can correlate the selected detachment with
/// <c>modifierGroups.conditions[].childId</c> values.
/// </summary>
public class BsDataDetachmentInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}
