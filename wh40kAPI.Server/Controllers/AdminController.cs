using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;
using wh40kAPI.Server.Middleware;
using wh40kAPI.Server.Services;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/wh40k/[controller]")]
[EnableRateLimiting("admin")]
public class AdminController(AppDbContext db, DataImportService importService) : ControllerBase
{
    /// <summary>
    /// Download the wahapedia Export Data Specs Excel file, follow its CSV links,
    /// and import all WH40K data into the database.
    /// Requires X-Admin-Password header.
    /// </summary>
    [HttpPost("import")]
    [AdminAuth]
    public async Task<IActionResult> ImportData()
    {
        await importService.ImportFromWahapediaAsync();
        return Ok(new { message = "Data imported successfully." });
    }

    /// <summary>
    /// Check database status: last update time and record counts.
    /// </summary>
    [HttpGet("status")]
    [AdminAuth]
    public async Task<IActionResult> Status()
    {
        var lastUpdate = await db.LastUpdates.AsNoTracking().FirstOrDefaultAsync();
        return Ok(new
        {
            lastUpdate = lastUpdate?.UpdatedAt,
            factions = await db.Factions.CountAsync(),
            datasheets = await db.Datasheets.CountAsync(),
            abilities = await db.Abilities.CountAsync(),
            detachments = await db.Detachments.CountAsync(),
            stratagems = await db.Stratagems.CountAsync(),
            enhancements = await db.Enhancements.CountAsync(),
            sources = await db.Sources.CountAsync(),
        });
    }

    /// <summary>
    /// Verify admin password without doing anything else.
    /// </summary>
    [HttpPost("verify")]
    [AdminAuth]
    public IActionResult Verify() => Ok(new { authenticated = true });
}
