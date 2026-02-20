namespace wh40kAPI.Server.Models;

public class DatasheetKeyword
{
    public string DatasheetId { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? IsFactionKeyword { get; set; }
}
