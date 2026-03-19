using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Middleware;
using wh40kAPI.Server.Services;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/bsdata/admin")]
[EnableRateLimiting("admin")]
public class BsDataAdminController(BsDataDbContext db, BsDataImportService importService, ILogger<BsDataAdminController> logger) : ControllerBase
{
    /// <summary>
    /// Trigger import of WH40K BSData from the BSData/wh40k-10e GitHub repository.
    /// Requires X-Admin-Password header.
    /// </summary>
    [HttpPost("import")]
    [AdminAuth]
    public async Task<IActionResult> Import()
    {
        try
        {
            int units = await importService.ImportAsync();
            return Ok(new { message = $"BSData imported successfully. Units: {units}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BSData import failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Check BSData database status: record counts.
    /// </summary>
    [HttpGet("status")]
    [AdminAuth]
    public async Task<IActionResult> Status()
    {
        return Ok(new
        {
            catalogues = await db.Catalogues.CountAsync(),
            catalogueLinks = await db.CatalogueLinks.CountAsync(),
            rules = await db.Rules.CountAsync(),
            units = await db.Units.CountAsync(),
            profiles = await db.Profiles.CountAsync(),
            unitCategories = await db.UnitCategories.CountAsync(),
        });
    }
}
