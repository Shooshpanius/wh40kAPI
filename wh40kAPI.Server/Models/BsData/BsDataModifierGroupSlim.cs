namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight projection of <see cref="BsDataModifierGroup"/> used by the
/// <c>/unitsList</c> endpoint.  Only the fields actually consumed by the
/// client are included; <c>Id</c> and <c>UnitId</c> are omitted to
/// reduce payload size.
/// </summary>
public class BsDataModifierGroupSlim
{
    /// <summary>JSON array of modifier objects (id, field, type, value).</summary>
    public string? Modifiers { get; set; }
    /// <summary>JSON array of condition objects (id, field, scope, value, type, childId).</summary>
    public string? Conditions { get; set; }
}
