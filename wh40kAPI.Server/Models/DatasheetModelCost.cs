namespace wh40kAPI.Server.Models;

public class DatasheetModelCost
{
    public string DatasheetId { get; set; } = string.Empty;
    public int Line { get; set; }
    public string? Description { get; set; }
    public string? Cost { get; set; }
}
