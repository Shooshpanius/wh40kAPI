namespace wh40kAPI.Server.Models;

public class DatasheetOption
{
    public string DatasheetId { get; set; } = string.Empty;
    public int Line { get; set; }
    public string? Button { get; set; }
    public string? Description { get; set; }
}
