using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "bsdata")]
[Route("api/bsdata/catalogues")]
public class BsDataCataloguesController(BsDataDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<BsDataCatalogue>> GetAll() =>
        await db.Catalogues.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<BsDataCatalogue>> GetById(string id)
    {
        var item = await db.Catalogues.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/units")]
    public async Task<IEnumerable<BsDataUnit>> GetUnits(string id) =>
        await db.Units.AsNoTracking()
            .Include(u => u.Categories.Where(c => c.Primary))
            .Include(u => u.InfoLinks)
            .Include(u => u.EntryLinks)
            .Where(u => u.CatalogueId == id)
            .OrderBy(u => u.Name)
            .ToListAsync();

    [HttpGet("{id}/rules")]
    public async Task<ActionResult<IEnumerable<BsDataRule>>> GetRules(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id))
            return NotFound();

        return await db.Rules.AsNoTracking()
            .Where(r => r.CatalogueId == id)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    [HttpGet("{id}/links")]
    public async Task<ActionResult<IEnumerable<BsDataCatalogueLink>>> GetLinks(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id))
            return NotFound();

        return await db.CatalogueLinks.AsNoTracking()
            .Where(l => l.CatalogueId == id)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }
}
