namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight projection of <see cref="BsDataUnitCategory"/> used by the
/// <c>/unitsList</c> endpoint.  Only the fields actually consumed by the
/// client are included; <c>Id</c> and <c>UnitId</c> are omitted to
/// reduce payload size.
/// </summary>
public class BsDataUnitCategorySlim
{
    public string Name { get; set; } = string.Empty;
    public bool Primary { get; set; }
}
