namespace wh40kAPI.Server.Models;

public class DatasheetWargear
{
    public string DatasheetId { get; set; } = string.Empty;
    public int Line { get; set; }
    public int LineInWargear { get; set; }
    public string? Dice { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Range { get; set; }
    public string? Type { get; set; }
    public string? A { get; set; }
    public string? BsWs { get; set; }
    public string? S { get; set; }
    public string? AP { get; set; }
    public string? D { get; set; }
}
