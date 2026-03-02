using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "bsdata")]
[Route("api/bsdata/units")]
public class BsDataUnitsController(BsDataDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<BsDataUnit>> GetAll([FromQuery] string? catalogueId, [FromQuery] string? category)
    {
        IQueryable<BsDataUnit> query = db.Units.AsNoTracking()
            .Include(u => u.Categories.Where(c => c.Primary))
            .Where(u => catalogueId == null || u.CatalogueId == catalogueId);

        if (category != null)
        {
            var unitIdsWithCategory = db.UnitCategories
                .Where(c => c.Name == category)
                .Select(c => c.UnitId);
            query = query.Where(u => unitIdsWithCategory.Contains(u.Id));
        }

        return await query.OrderBy(u => u.Name).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BsDataUnit>> GetById(string id)
    {
        var item = await db.Units
            .Include(u => u.Categories)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/profiles")]
    public async Task<IEnumerable<BsDataProfile>> GetProfiles(string id) =>
        await db.Profiles.AsNoTracking()
            .Where(p => p.UnitId == id)
            .ToListAsync();

    [HttpGet("{id}/categories")]
    public async Task<ActionResult<IEnumerable<BsDataUnitCategory>>> GetCategories(string id)
    {
        if (!await db.Units.AnyAsync(u => u.Id == id))
            return NotFound();

        return await db.UnitCategories.AsNoTracking()
            .Where(c => c.UnitId == id)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    [HttpGet("{id}/infolinks")]
    public async Task<ActionResult<IEnumerable<BsDataInfoLink>>> GetInfoLinks(string id)
    {
        if (!await db.Units.AnyAsync(u => u.Id == id))
            return NotFound();

        return await db.InfoLinks.AsNoTracking()
            .Where(l => l.UnitId == id)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    [HttpGet("{id}/entrylinks")]
    public async Task<ActionResult<IEnumerable<BsDataEntryLink>>> GetEntryLinks(string id)
    {
        if (!await db.Units.AnyAsync(u => u.Id == id))
            return NotFound();

        return await db.EntryLinks.AsNoTracking()
            .Where(l => l.UnitId == id)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    [HttpGet("{id}/constraints")]
    public async Task<ActionResult<IEnumerable<BsDataConstraint>>> GetConstraints(string id)
    {
        if (!await db.Units.AnyAsync(u => u.Id == id))
            return NotFound();

        return await db.Constraints.AsNoTracking()
            .Where(c => c.UnitId == id)
            .ToListAsync();
    }

    [HttpGet("{id}/modifiergroups")]
    public async Task<ActionResult<IEnumerable<BsDataModifierGroup>>> GetModifierGroups(string id)
    {
        if (!await db.Units.AnyAsync(u => u.Id == id))
            return NotFound();

        return await db.ModifierGroups.AsNoTracking()
            .Where(g => g.UnitId == id)
            .ToListAsync();
    }
}
