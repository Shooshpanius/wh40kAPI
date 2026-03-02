namespace wh40kAPI.Server.Models.BsData;

public class BsDataConstraint
{
    public string Id { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? Scope { get; set; }
    public string? Value { get; set; }
    public bool PercentValue { get; set; }
    public bool Shared { get; set; }
    public bool IncludeChildSelections { get; set; }
    public bool IncludeChildForces { get; set; }
    public string? ChildId { get; set; }
    public string? Type { get; set; }
}
