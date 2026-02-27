using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "wh40k")]
[Route("api/wh40k/[controller]")]
public class DatasheetsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Datasheet>> GetAll([FromQuery] string? factionId) =>
        await db.Datasheets.AsNoTracking()
            .Where(d => factionId == null || d.FactionId == factionId)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Datasheet>> GetById(string id)
    {
        var item = await db.Datasheets.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/abilities")]
    public async Task<IEnumerable<DatasheetAbility>> GetAbilities(string id) =>
        await db.DatasheetAbilities.AsNoTracking()
            .Where(a => a.DatasheetId == id)
            .ToListAsync();

    [HttpGet("{id}/models")]
    public async Task<IEnumerable<DatasheetModel>> GetModels(string id) =>
        await db.DatasheetModels.AsNoTracking()
            .Where(m => m.DatasheetId == id)
            .ToListAsync();

    [HttpGet("{id}/wargear")]
    public async Task<IEnumerable<DatasheetWargear>> GetWargear(string id) =>
        await db.DatasheetWargears.AsNoTracking()
            .Where(w => w.DatasheetId == id)
            .ToListAsync();

    [HttpGet("{id}/keywords")]
    public async Task<IEnumerable<DatasheetKeyword>> GetKeywords(string id) =>
        await db.DatasheetKeywords.AsNoTracking()
            .Where(k => k.DatasheetId == id)
            .ToListAsync();

    [HttpGet("{id}/unit-composition")]
    public async Task<IEnumerable<DatasheetUnitComposition>> GetUnitComposition(string id) =>
        await db.DatasheetUnitCompositions.AsNoTracking()
            .Where(u => u.DatasheetId == id)
            .ToListAsync();

    [HttpGet("{id}/options")]
    public async Task<IEnumerable<DatasheetOption>> GetOptions(string id) =>
        await db.DatasheetOptions.AsNoTracking()
            .Where(o => o.DatasheetId == id)
            .ToListAsync();

    [HttpGet("{id}/model-costs")]
    public async Task<IEnumerable<DatasheetModelCost>> GetModelCosts(string id) =>
        await db.DatasheetModelCosts.AsNoTracking()
            .Where(c => c.DatasheetId == id)
            .ToListAsync();
}
