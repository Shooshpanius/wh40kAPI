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

    [HttpGet("{id}/cost-tiers")]
    public async Task<ActionResult<IEnumerable<BsDataCostTier>>> GetCostTiers(string id)
    {
        if (!await db.Units.AnyAsync(u => u.Id == id))
            return NotFound();

        return await db.CostTiers.AsNoTracking()
            .Where(t => t.UnitId == id)
            .OrderBy(t => t.MinModels)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a full <see cref="BsDataUnitNode"/> for the unit with <paramref name="id"/>,
    /// including its profiles (unit stats and weapon stats) and direct children (upgrade-nodes
    /// such as weapons) with their profiles and infoLinks.
    /// Use this endpoint to load a complete datasheet for a single unit on demand,
    /// instead of fetching the entire faction tree via <c>/fractions/{id}/unitsTree</c>.
    /// </summary>
    [HttpGet("{id}/fullNode")]
    public async Task<ActionResult<BsDataUnitNode>> GetFullNode(string id)
    {
        var unit = await db.Units.AsNoTracking()
            .Include(u => u.Categories)
            .Include(u => u.InfoLinks)
            .Include(u => u.EntryLinks)
            .Include(u => u.CostTiers)
            .Include(u => u.ModifierGroups)
            .Include(u => u.Profiles)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (unit is null) return NotFound();

        var node = BsDataUnitNode.FromUnit(unit);

        var children = await db.Units.AsNoTracking()
            .Include(u => u.InfoLinks)
            .Include(u => u.Profiles)
            .Where(u => u.ParentId == id)
            .OrderBy(u => u.Name)
            .ToListAsync();
        foreach (var child in children)
            node.Children.Add(BsDataUnitNode.FromUnit(child));

        return Ok(node);
    }
}
