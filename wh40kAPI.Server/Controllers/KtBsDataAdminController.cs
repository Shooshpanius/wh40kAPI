using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Middleware;
using wh40kAPI.Server.Services;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/ktbsdata/admin")]
public class KtBsDataAdminController(KtBsDataDbContext db, KtBsDataImportService importService) : ControllerBase
{
    /// <summary>
    /// Trigger import of Kill Team BSData from the BSData/wh40k-killteam GitHub repository.
    /// Requires X-Admin-Password header.
    /// </summary>
    [HttpPost("import")]
    [AdminAuth]
    public async Task<IActionResult> Import()
    {
        int units = await importService.ImportAsync();
        return Ok(new { message = $"Kill Team BSData imported successfully. Units: {units}" });
    }

    /// <summary>
    /// Check Kill Team BSData database status: record counts.
    /// </summary>
    [HttpGet("status")]
    [AdminAuth]
    public async Task<IActionResult> Status()
    {
        return Ok(new
        {
            catalogues = await db.Catalogues.CountAsync(),
            units = await db.Units.CountAsync(),
            profiles = await db.Profiles.CountAsync(),
        });
    }
}
