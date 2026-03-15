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
            .Include(u => u.ModifierGroups)
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
    /// Returns the list of detachments available for the given fraction,
    /// each with its BSData entry <c>id</c> and human-readable <c>name</c>.
    /// The <c>id</c> matches the <c>childId</c> used in
    /// <c>modifierGroups.conditions</c> of unit tree nodes.
    /// </summary>
    [HttpGet("{id}/detachments")]
    public async Task<ActionResult<IEnumerable<BsDataDetachmentInfo>>> GetDetachments(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        // Step 1: root Detachment nodes: entryType="upgrade", parentId=null, category="Configuration"
        // First search the faction's own catalogue to avoid picking up detachments
        // from linked shared catalogues (e.g. generic CSM detachments appearing for Death Guard).
        var detachmentRootIds = await db.Units
            .AsNoTracking()
            .Where(u => u.CatalogueId == id
                     && u.EntryType == "upgrade"
                     && u.ParentId == null
                     && u.Categories.Any(c => c.Name == "Configuration"))
            .Select(u => u.Id)
            .ToListAsync();

        // Fallback: some factions (e.g. Chaos Daemons) define their Detachment entry
        // in a shared library catalogue rather than directly in their own catalogue.
        // First try catalogue-level entryLinks — an explicit list of shared entries
        // imported by the faction catalogue.  This is more precise than a recursive
        // catalogue search because it won't pick up detachment roots from other linked
        // library catalogues (e.g. Chaos Knights Library linked from Chaos Daemons
        // with importRootEntries=true).
        if (detachmentRootIds.Count == 0)
        {
            var directTargetIds = await db.CatalogueLevelEntryLinks
                .AsNoTracking()
                .Where(l => l.CatalogueId == id)
                .Select(l => l.TargetId)
                .ToListAsync();

            if (directTargetIds.Count > 0)
            {
                detachmentRootIds = await db.Units
                    .AsNoTracking()
                    .Where(u => directTargetIds.Contains(u.Id)
                             && u.EntryType == "upgrade"
                             && u.ParentId == null
                             && u.Categories.Any(c => c.Name == "Configuration"))
                    .Select(u => u.Id)
                    .ToListAsync();
            }
        }

        // Last-resort fallback: recursively follow catalogueLinks where
        // importRootEntries=true.  Only follow importRootEntries=true links so that
        // faction catalogues linked without importRootEntries (e.g. Chaos Space
        // Marines linked from Chaos Daemons) do not contribute their own detachment
        // roots.
        if (detachmentRootIds.Count == 0)
        {
            var linkedCatalogueIds = await CollectCatalogueIdsAsync(id, importRootEntriesOnly: true);
            detachmentRootIds = await db.Units
                .AsNoTracking()
                .Where(u => linkedCatalogueIds.Contains(u.CatalogueId)
                         && u.EntryType == "upgrade"
                         && u.ParentId == null
                         && u.Categories.Any(c => c.Name == "Configuration"))
                .Select(u => u.Id)
                .ToListAsync();
        }

        if (detachmentRootIds.Count == 0)
            return Ok(Array.Empty<BsDataDetachmentInfo>());

        // Step 2: child selectionEntryGroup nodes
        var groupIds = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId != null
                     && detachmentRootIds.Contains(u.ParentId)
                     && u.EntryType == "selectionEntryGroup")
            .Select(u => u.Id)
            .ToListAsync();

        if (groupIds.Count == 0)
            return Ok(Array.Empty<BsDataDetachmentInfo>());

        // Step 3: detachment entries — children of selectionEntryGroup
        var detachments = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId != null && groupIds.Contains(u.ParentId))
            .OrderBy(u => u.Name)
            .Select(u => new BsDataDetachmentInfo { Id = u.Id, Name = u.Name })
            .ToListAsync();

        return Ok(detachments.DistinctBy(d => d.Id));
    }

    /// <summary>
    /// Collects all catalogue IDs reachable from <paramref name="rootId"/>
    /// by following catalogueLinks recursively.
    /// Loads all catalogue links in a single query then traverses them in memory.
    /// </summary>
    /// <param name="rootId">The starting catalogue ID.</param>
    /// <param name="importRootEntriesOnly">
    /// When <c>true</c>, only follows links where <c>importRootEntries=true</c>,
    /// preventing detachments from catalogues that don't export their root entries
    /// from appearing in results (e.g. Chaos Space Marines linked from Chaos Daemons).
    /// </param>
    private async Task<HashSet<string>> CollectCatalogueIdsAsync(string rootId, bool importRootEntriesOnly = false)
    {
        // Load all links once to avoid N+1 queries during traversal.
        var allLinks = await db.CatalogueLinks.AsNoTracking()
            .Select(l => new { l.CatalogueId, l.TargetId, l.ImportRootEntries })
            .ToListAsync();

        var linkMap = allLinks
            .Where(l => !importRootEntriesOnly || l.ImportRootEntries)
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
