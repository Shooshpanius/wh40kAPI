namespace wh40kAPI.Server.Models;

public class DatasheetAbility
{
    public string DatasheetId { get; set; } = string.Empty;
    public int Line { get; set; }
    public string? AbilityId { get; set; }
    public string? Model { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Parameter { get; set; }
}
