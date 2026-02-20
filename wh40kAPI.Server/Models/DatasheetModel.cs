namespace wh40kAPI.Server.Models;

public class DatasheetModel
{
    public string DatasheetId { get; set; } = string.Empty;
    public int Line { get; set; }
    public string? Name { get; set; }
    public string? M { get; set; }
    public string? T { get; set; }
    public string? Sv { get; set; }
    public string? InvSv { get; set; }
    public string? InvSvDescr { get; set; }
    public string? W { get; set; }
    public string? Ld { get; set; }
    public string? OC { get; set; }
    public string? BaseSize { get; set; }
    public string? BaseSizeDescr { get; set; }
}
