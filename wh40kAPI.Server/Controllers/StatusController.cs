using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/status")]
public class StatusController(AppDbContext appDb, BsDataDbContext bsDataDb, KtBsDataDbContext ktBsDataDb) : ControllerBase
{
    /// <summary>
    /// Returns the last import date/time for each of the three data sources.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var wh40kLastUpdate = await appDb.LastUpdates.AsNoTracking()
            .Select(u => u.UpdatedAt)
            .FirstOrDefaultAsync();

        var bsDataLastUpdate = await bsDataDb.Catalogues.AsNoTracking()
            .Select(c => (DateTime?)c.FetchedAt)
            .MaxAsync();

        var ktBsDataLastUpdate = await ktBsDataDb.Catalogues.AsNoTracking()
            .Select(c => (DateTime?)c.FetchedAt)
            .MaxAsync();

        return Ok(new
        {
            wh40k = wh40kLastUpdate,
            bsData = bsDataLastUpdate,
            ktBsData = ktBsDataLastUpdate,
        });
    }
}
