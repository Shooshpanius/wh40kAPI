using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.KtBsData;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/ktbsdata-catalogues")]
public class KtBsDataCataloguesController(KtBsDataDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<KtBsDataCatalogue>> GetAll() =>
        await db.Catalogues.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<KtBsDataCatalogue>> GetById(string id)
    {
        var item = await db.Catalogues.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/units")]
    public async Task<IEnumerable<KtBsDataUnit>> GetUnits(string id) =>
        await db.Units.AsNoTracking()
            .Where(u => u.CatalogueId == id)
            .OrderBy(u => u.Name)
            .ToListAsync();
}
