using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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

        // Load catalogue-level entryLinks (root entryLinks) with detachment conditions
        // for all catalogues in the hierarchy.  These are used below to mark root nodes
        // that are only available when a specific detachment is selected.
        var catalogueLevelEntryLinksWithConditions = await db.CatalogueLevelEntryLinks
            .AsNoTracking()
            .Where(l => catalogueIds.Contains(l.CatalogueId)
                     && l.DetachmentModifiers != null
                     && l.DetachmentConditions != null)
            .ToListAsync();
        // Build a lookup: targetId → (DetachmentModifiers, DetachmentConditions).
        // When the same target is referenced by multiple catalogue-level entryLinks
        // (e.g. via different catalogues in the hierarchy), the first one wins.
        var catalogueLevelDetachmentByTarget = catalogueLevelEntryLinksWithConditions
            .GroupBy(l => l.TargetId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringComparer.OrdinalIgnoreCase);

        var roots = new List<BsDataUnitNode>();
        foreach (var node in nodeById.Values)
        {
            if (node.ParentId is not null && nodeById.TryGetValue(node.ParentId, out var parent))
                parent.Children.Add(node);
            else if (!entryLinkTargets.Contains(node.Id))
            {
                // When the unit is added via a root catalogue-level entryLink that carries
                // a detachment dependency, present it as hidden by default with a detachment-
                // unlock modifierGroup — mirroring the same logic applied to unit-level entryLinks.
                if (catalogueLevelDetachmentByTarget.TryGetValue(node.Id, out var rootLink)
                    && rootLink.DetachmentModifiers is not null
                    && rootLink.DetachmentConditions is not null)
                    roots.Add(BsDataUnitNode.WithDetachmentDependency(node, rootLink.DetachmentModifiers, rootLink.DetachmentConditions));
                else
                    roots.Add(node);
            }
            // else: shared entry reachable only via entryLinks — attached below
        }

        // Populate RequiredUpgrades for nodes that have upgrade children with minInRoster > 0
        // and a detachment-hide condition.  This is done after the parent-child hierarchy is
        // built so that Children collections are complete before we inspect them.
        foreach (var node in nodeById.Values)
        {
            var requiredUpgrades = node.Children
                .Where(c => c.EntryType == "upgrade" && c.MinInRoster > 0)
                .Select(c => new BsDataRequiredUpgrade
                {
                    Id = c.Id,
                    Name = c.Name,
                    MinInRoster = c.MinInRoster,
                    MaxInRoster = c.MaxInRoster,
                    RequiredDetachmentId = ExtractRequiredDetachmentId(c.ModifierGroups),
                })
                .Where(r => r.RequiredDetachmentId is not null)
                .ToList();

            if (requiredUpgrades.Count > 0)
                node.RequiredUpgrades = requiredUpgrades;
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

                // When the entryLink carries a detachment dependency (hide unless a specific
                // detachment is active), present the linked unit as hidden by default with an
                // unlock modifierGroup — rather than using the shared target node, which has
                // hidden=false based on the underlying selectionEntry definition.
                if (link.DetachmentModifiers != null && link.DetachmentConditions != null)
                    node.Children.Add(BsDataUnitNode.WithDetachmentDependency(target, link.DetachmentModifiers, link.DetachmentConditions));
                else
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

            // When multiple detachment roots are found from different linked catalogues
            // (e.g. Space Marines, Agents of the Imperium, and Imperial Knights Library
            // are all reachable from Blood Angels), keep only the "topmost" roots —
            // those whose catalogue is NOT itself reachable via catalogueLinks from any
            // other catalogue that also owns a detachment root in this set.
            //
            // Example: Space Marines links to both Agents of the Imperium and Imperial
            // Knights Library, so those two are "lower" in the hierarchy.  Filtering
            // them out leaves only the Space Marines root, which is the correct one
            // for Blood Angels and every other Space Marines sub-chapter.
            if (detachmentRootIds.Count > 1)
            {
                // Find which catalogue each found root belongs to.
                var rootCatalogueMap = await db.Units
                    .AsNoTracking()
                    .Where(u => detachmentRootIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.CatalogueId })
                    .ToListAsync();

                var rootCatalogueIds = rootCatalogueMap
                    .Select(r => r.CatalogueId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (rootCatalogueIds.Count > 1)
                {
                    // Among those catalogues, find which ones are linked-to by others
                    // in the same set (directly or transitively).
                    var lowerCatalogueIds = await db.CatalogueLinks
                        .AsNoTracking()
                        .Where(l => rootCatalogueIds.Contains(l.CatalogueId)
                                 && rootCatalogueIds.Contains(l.TargetId))
                        .Select(l => l.TargetId)
                        .Distinct()
                        .ToListAsync();

                    if (lowerCatalogueIds.Count > 0 && lowerCatalogueIds.Count < rootCatalogueIds.Count)
                    {
                        var lowerSet = new HashSet<string>(lowerCatalogueIds, StringComparer.OrdinalIgnoreCase);
                        detachmentRootIds = rootCatalogueMap
                            .Where(r => !lowerSet.Contains(r.CatalogueId))
                            .Select(r => r.Id)
                            .ToList();
                    }
                }
            }
        }

        if (detachmentRootIds.Count == 0)
            return Ok(Array.Empty<BsDataDetachmentInfo>());

        // Step 2: child selectionEntryGroup nodes — either nested directly inside the
        // detachment root, or referenced from it via an entryLink.
        // Some factions (e.g. Space Marines) store the Detachment selection group in
        // sharedSelectionEntryGroups (parentId=null) and link it from the Detachment
        // root via an entryLink rather than nesting it directly.
        var groupIds = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId != null
                     && detachmentRootIds.Contains(u.ParentId)
                     && u.EntryType == "selectionEntryGroup")
            .Select(u => u.Id)
            .ToListAsync();

        // Also follow entryLinks of type "selectionEntryGroup" from the detachment roots.
        var linkedGroupIds = await db.EntryLinks
            .AsNoTracking()
            .Where(l => detachmentRootIds.Contains(l.UnitId) && l.Type == "selectionEntryGroup")
            .Select(l => l.TargetId)
            .ToListAsync();
        if (linkedGroupIds.Count > 0)
            groupIds.AddRange(linkedGroupIds);

        if (groupIds.Count == 0)
            return Ok(Array.Empty<BsDataDetachmentInfo>());

        // Step 3: detachment entries — children of selectionEntryGroup
        var detachments = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId != null && groupIds.Contains(u.ParentId))
            .OrderBy(u => u.Name)
            .Select(u => new BsDataDetachmentInfo { Id = u.Id, Name = u.Name })
            .ToListAsync();

        if (detachments.Count == 0)
            return Ok(Array.Empty<BsDataDetachmentInfo>());

        // Step 4: filter detachment entries by their hidden-modifier visibility conditions.
        // In BSData, each detachment entry that is not available to all factions carries a
        // <modifier type="set" field="hidden" value="true"> with a
        // <condition scope="primary-catalogue"> child.  Two patterns:
        //   • notInstanceOf childId=X → entry is hidden when the primary catalogue is NOT X,
        //     i.e. the detachment is exclusive to catalogue X.
        //   • instanceOf   childId=X → entry is hidden when the primary catalogue IS X,
        //     i.e. the detachment is excluded from catalogue X.
        // An entry with no such conditions is visible to all.
        var detachmentIds = detachments.Select(d => d.Id).ToList();
        var visibilities = await db.DetachmentVisibilities
            .AsNoTracking()
            .Where(v => detachmentIds.Contains(v.UnitId))
            .ToListAsync();

        if (visibilities.Count > 0)
        {
            var visibilityLookup = visibilities.ToLookup(v => v.UnitId, StringComparer.OrdinalIgnoreCase);
            detachments = detachments
                .Where(d =>
                {
                    var conditions = visibilityLookup[d.Id];
                    foreach (var cond in conditions)
                    {
                        bool catalogueMatches = string.Equals(id, cond.CatalogueId, StringComparison.OrdinalIgnoreCase);
                        // notInstanceOf: hidden when catalogue is NOT the specified one → exclude unless id matches
                        if (string.Equals(cond.ConditionType, "notInstanceOf", StringComparison.OrdinalIgnoreCase) && !catalogueMatches)
                            return false;
                        // instanceOf: hidden when catalogue IS the specified one → exclude when id matches
                        if (string.Equals(cond.ConditionType, "instanceOf", StringComparison.OrdinalIgnoreCase) && catalogueMatches)
                            return false;
                    }
                    return true;
                })
                .ToList();
        }

        return Ok(detachments.DistinctBy(d => d.Id));
    }

    /// <summary>
    /// Inspects a node's <see cref="BsDataModifierGroup"/> collection and returns the
    /// detachment entry id that is required to "un-hide" the entry, or <c>null</c> if no
    /// such dependency is found.
    /// <para>
    /// The pattern looked for: a modifier group whose <c>modifiers</c> JSON contains an entry
    /// with <c>field="hidden"</c> and <c>value="true"</c>, and whose <c>conditions</c> JSON
    /// contains a condition with a non-empty <c>childId</c> — that childId is the detachment id.
    /// </para>
    /// </summary>
    private static string? ExtractRequiredDetachmentId(ICollection<BsDataModifierGroup> modifierGroups)
    {
        foreach (var mg in modifierGroups)
        {
            if (mg.Modifiers is null || mg.Conditions is null)
                continue;

            try
            {
                using var modDoc = JsonDocument.Parse(mg.Modifiers);
                var setsHiddenTrue = modDoc.RootElement.EnumerateArray().Any(m =>
                    m.TryGetProperty("field", out var f) &&
                    f.ValueKind == JsonValueKind.String &&
                    string.Equals(f.GetString(), "hidden", StringComparison.OrdinalIgnoreCase) &&
                    m.TryGetProperty("value", out var v) &&
                    v.ValueKind == JsonValueKind.String &&
                    string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase));

                if (!setsHiddenTrue)
                    continue;

                using var condDoc = JsonDocument.Parse(mg.Conditions);
                foreach (var cond in condDoc.RootElement.EnumerateArray())
                {
                    if (cond.TryGetProperty("childId", out var childId) &&
                        childId.ValueKind == JsonValueKind.String &&
                        childId.GetString() is { Length: > 0 } detachmentId)
                        return detachmentId;
                }
            }
            catch (JsonException)
            {
                // Skip malformed JSON — should not happen with well-formed import data.
            }
        }

        return null;
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
