using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/bsdata/units")]
public class BsDataUnitsController(BsDataDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<BsDataUnit>> GetAll([FromQuery] string? catalogueId) =>
        await db.Units.AsNoTracking()
            .Where(u => catalogueId == null || u.CatalogueId == catalogueId)
            .OrderBy(u => u.Name)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<BsDataUnit>> GetById(string id)
    {
        var item = await db.Units.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/profiles")]
    public async Task<IEnumerable<BsDataProfile>> GetProfiles(string id) =>
        await db.Profiles.AsNoTracking()
            .Where(p => p.UnitId == id)
            .ToListAsync();
}
