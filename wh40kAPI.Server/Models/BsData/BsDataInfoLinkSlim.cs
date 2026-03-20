namespace wh40kAPI.Server.Models.BsData;

/// <summary>
/// Lightweight projection of <see cref="BsDataInfoLink"/> used by the
/// <c>/unitsList</c> endpoint.  Only the fields actually consumed by the
/// client are included; <c>Id</c> and <c>TargetId</c> are omitted to
/// reduce payload size.
/// </summary>
public class BsDataInfoLinkSlim
{
    public string? Type { get; set; }
    public string Name { get; set; } = string.Empty;
}
