using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.KtBsData;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/ktbsdata-units")]
public class KtBsDataUnitsController(KtBsDataDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<KtBsDataUnit>> GetAll([FromQuery] string? catalogueId) =>
        await db.Units.AsNoTracking()
            .Where(u => catalogueId == null || u.CatalogueId == catalogueId)
            .OrderBy(u => u.Name)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<KtBsDataUnit>> GetById(string id)
    {
        var item = await db.Units.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/profiles")]
    public async Task<IEnumerable<KtBsDataProfile>> GetProfiles(string id) =>
        await db.Profiles.AsNoTracking()
            .Where(p => p.UnitId == id)
            .ToListAsync();
}
