namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// An upgrade child entry that is required by a specific detachment.
/// Surfaces roster-level min/max constraints for upgrade sub-entries so that
/// API consumers can display detachment-conditional mandatory upgrades even
/// for parent entries whose type (e.g. "model") is not normally tree-expanded.
/// </summary>
public class BsDataRequiredUpgrade
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? MinInRoster { get; set; }
    public int? MaxInRoster { get; set; }
    /// <summary>
    /// The detachment entry id that activates this upgrade requirement.
    /// Derived from the upgrade's modifierGroup that hides it when the
    /// detachment is absent (<c>field="hidden"</c>, <c>value="true"</c>).
    /// </summary>
    public string? RequiredDetachmentId { get; set; }
}
