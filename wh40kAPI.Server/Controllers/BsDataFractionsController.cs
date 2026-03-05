using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "bsdata")]
[Route("api/bsdata/fractions")]
public class BsDataFractionsController(BsDataDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns all fractions (catalogues with library=false).
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<BsDataCatalogue>> GetAll() =>
        await db.Catalogues.AsNoTracking()
            .Where(c => !c.Library)
            .OrderBy(c => c.Name)
            .ToListAsync();

    /// <summary>
    /// Returns a single fraction by id (must have library=false).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BsDataCatalogue>> GetById(string id)
    {
        var item = await db.Catalogues.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.Library);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Returns all units belonging to the fraction — units from the fraction's own catalogue
    /// plus units from every catalogue reachable via catalogueLinks (resolved recursively).
    /// </summary>
    [HttpGet("{id}/units")]
    public async Task<ActionResult<IEnumerable<BsDataUnit>>> GetUnits(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        var catalogueIds = await CollectCatalogueIdsAsync(id);

        var units = await db.Units.AsNoTracking()
            .Include(u => u.Categories.Where(c => c.Primary))
            .Include(u => u.InfoLinks)
            .Include(u => u.EntryLinks)
            .Where(u => catalogueIds.Contains(u.CatalogueId))
            .OrderBy(u => u.Name)
            .ToListAsync();

        return Ok(units);
    }

    /// <summary>
    /// Returns all units belonging to the fraction with populated cost-tier information.
    /// Same as /units but each unit also includes its CostTiers (points cost per squad size).
    /// </summary>
    [HttpGet("{id}/unitsWithCosts")]
    public async Task<ActionResult<IEnumerable<BsDataUnit>>> GetUnitsWithCosts(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        var catalogueIds = await CollectCatalogueIdsAsync(id);

        var units = await db.Units.AsNoTracking()
            .Include(u => u.Categories.Where(c => c.Primary))
            .Include(u => u.InfoLinks)
            .Include(u => u.EntryLinks)
            .Include(u => u.CostTiers)
            .Where(u => catalogueIds.Contains(u.CatalogueId))
            .OrderBy(u => u.Name)
            .ToListAsync();

        return Ok(units);
    }

    /// <summary>
    /// Collects all catalogue IDs reachable from <paramref name="rootId"/>
    /// by following catalogueLinks recursively.
    /// Loads all catalogue links in a single query then traverses them in memory.
    /// </summary>
    private async Task<HashSet<string>> CollectCatalogueIdsAsync(string rootId)
    {
        // Load all links once to avoid N+1 queries during traversal.
        var allLinks = await db.CatalogueLinks.AsNoTracking()
            .Select(l => new { l.CatalogueId, l.TargetId })
            .ToListAsync();

        var linkMap = allLinks
            .GroupBy(l => l.CatalogueId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(l => l.TargetId).ToList(), StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootId };
        var queue = new Queue<string>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!linkMap.TryGetValue(current, out var linkedIds))
                continue;

            foreach (var targetId in linkedIds)
            {
                if (visited.Add(targetId))
                    queue.Enqueue(targetId);
            }
        }

        return visited;
    }
}
