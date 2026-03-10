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
    /// Returns all units belonging to the fraction arranged as a hierarchy.
    /// Same data as /unitsWithCosts but nested by parentId — entries whose parentId
    /// is null are returned as root nodes; their children are embedded recursively.
    /// Entry links are resolved: entries reachable only via entryLinks are embedded
    /// as children of the linking node rather than appearing as separate root nodes.
    /// </summary>
    [HttpGet("{id}/unitsTree")]
    public async Task<ActionResult<IEnumerable<BsDataUnitNode>>> GetUnitsTree(string id)
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

        // Convert every unit to a node and index by id for O(1) child lookup.
        var nodeById = units.ToDictionary(u => u.Id, BsDataUnitNode.FromUnit);

        // Collect all entry-link target IDs that are present in our node set.
        // Such entries are embedded as children of the linking node and should
        // not appear as independent root nodes.
        // This is O(total entryLinks across all units), which is well within the
        // scale of BSData catalogue data.
        var entryLinkTargets = new HashSet<string>(
            units.SelectMany(u => u.EntryLinks.Select(l => l.TargetId)),
            StringComparer.OrdinalIgnoreCase);

        var roots = new List<BsDataUnitNode>();
        foreach (var node in nodeById.Values)
        {
            if (node.ParentId is not null && nodeById.TryGetValue(node.ParentId, out var parent))
                parent.Children.Add(node);
            else if (!entryLinkTargets.Contains(node.Id))
                roots.Add(node);
            // else: shared entry reachable only via entryLinks — attached below
        }

        // Resolve entry links: attach each linked entry as a child of the linking node.
        foreach (var node in nodeById.Values)
        {
            foreach (var link in node.EntryLinks)
            {
                if (!nodeById.TryGetValue(link.TargetId, out var target)) continue;
                // Guard against trivial self-reference and immediate-parent cycle.
                // Deeper transitive cycles (A→B→C→A) are handled gracefully at
                // serialization time by ReferenceHandler.IgnoreCycles.
                if (target.Id == node.Id || target.Id == node.ParentId) continue;
                node.Children.Add(target);
            }
        }

        return Ok(roots);
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
