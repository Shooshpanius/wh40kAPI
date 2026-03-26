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
    /// Returns the fraction's own catalogue ids: the fraction's id itself plus every catalogueId
    /// reachable from it via catalogueLinks with importRootEntries=true (resolved recursively),
    /// and also via catalogueLinks pointing to library catalogues (library=true) regardless of
    /// the importRootEntries flag — library catalogues are internal shared dependencies
    /// (e.g. Chaos - Daemons Library for Death Guard) and are always considered "own".
    /// Full faction catalogues linked without importRootEntries (e.g. Chaos Space Marines
    /// linked from Chaos Daemons) are NOT included.
    /// </summary>
    [HttpGet("{id}/ownCatalogues")]
    public async Task<ActionResult<IEnumerable<string>>> GetOwnCatalogues(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        var ownIds = await CollectCatalogueIdsAsync(id, importRootEntriesOnly: true);

        // Filter out library catalogues that are actually "Allied" for this faction.
        var alliedIds = await DetermineAlliedLibraryCatalogueIdsAsync(id);
        foreach (var allied in alliedIds)
            ownIds.Remove(allied);

        return Ok(ownIds);
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
        return Ok(await BuildUnitsTreeAsync(catalogueIds, includeProfiles: true));
    }

    /// <summary>
    /// Returns a slim classification list for all units belonging to the fraction.
    /// Each item contains only: <c>id</c>, <c>catalogueId</c>, and <c>categories</c>.
    /// </summary>
    [HttpGet("{id}/units-classification")]
    public async Task<ActionResult<IEnumerable<BsDataUnitClassification>>> GetUnitsClassification(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        var catalogueIds = await CollectCatalogueIdsAsync(id);

        var units = await db.Units.AsNoTracking()
            .Where(u => catalogueIds.Contains(u.CatalogueId))
            .Include(u => u.Categories)
            .OrderBy(u => u.Name)
            .ToListAsync();

        return Ok(units.Select(BsDataUnitClassification.FromUnit));
    }

    /// <summary>
    /// Returns a flat list of all units belonging to the fraction.
    /// Each item contains only: <c>id</c>, <c>catalogueId</c>, <c>name</c>,
    /// <c>entryType</c>, <c>points</c>, <c>hidden</c>, <c>categories</c>, and
    /// <c>requiredUpgrades</c> (populated for root model nodes only).
    /// </summary>
    [HttpGet("{id}/unitsList")]
    public async Task<ActionResult<IEnumerable<BsDataUnitNodeLite>>> GetUnitsTreeLite(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        var catalogueIds = await CollectCatalogueIdsAsync(id);

        var units = await db.Units.AsNoTracking()
            .Where(u => catalogueIds.Contains(u.CatalogueId))
            .Include(u => u.Categories)
            .Include(u => u.ModifierGroups)
            .OrderBy(u => u.Name)
            .ToListAsync();

        // Build a lookup from parentId → upgrade children with minInRoster > 0.
        // Used to populate RequiredUpgrades on root model nodes (depth=0).
        var upgradeChildrenByParent = units
            .Where(u => u.ParentId is not null && u.EntryType == "upgrade" && u.MinInRoster > 0)
            .GroupBy(u => u.ParentId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = units.Select(u =>
        {
            var node = BsDataUnitNodeLite.FromUnit(u);
            if (u.ParentId is null && u.EntryType == "model"
                && upgradeChildrenByParent.TryGetValue(u.Id, out var upgradeChildren))
            {
                var requiredUpgrades = upgradeChildren
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
            return node;
        });

        return Ok(result);
    }

    /// <summary>
    /// Builds the units hierarchy for the given set of catalogue IDs.
    /// When <paramref name="includeProfiles"/> is <see langword="false"/> the profiles
    /// are not loaded from the database and every node's <c>Profiles</c> collection
    /// will be empty — suitable for lightweight list endpoints.
    /// </summary>
    /// <param name="catalogueIds">The set of catalogue IDs (faction catalogue plus all transitively linked catalogues) whose units are included in the tree.</param>
    /// <param name="includeProfiles">When <see langword="true"/> unit and weapon profiles are eagerly loaded; pass <see langword="false"/> for the lightweight <c>/unitsList</c> endpoint.</param>
    private async Task<List<BsDataUnitNode>> BuildUnitsTreeAsync(
        HashSet<string> catalogueIds, bool includeProfiles)
    {
        IQueryable<BsDataUnit> query = db.Units.AsNoTracking()
            .Include(u => u.Categories)
            .Include(u => u.InfoLinks)
            .Include(u => u.EntryLinks)
            .Include(u => u.CostTiers)
            .Include(u => u.ModifierGroups);

        if (includeProfiles)
            query = query.Include(u => u.Profiles);

        var units = await query
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

        return roots;
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
        // Collect catalogue-level entryLink targets once; reused by the sub-check below.
        var directTargetIds = new List<string>();
        if (detachmentRootIds.Count == 0)
        {
            directTargetIds = await db.CatalogueLevelEntryLinks
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

        // Sub-check: some factions (e.g. Adeptus Mechanicus) place the "Configuration"
        // categoryLink on the catalogue-level entryLink itself rather than on the target
        // selectionEntry.  In that case the entry won't carry a "Configuration" category
        // and the check above yields nothing.  Identify such roots by the presence of a
        // direct selectionEntryGroup child, which is the distinguishing structural trait
        // of a Detachment root entry.
        if (detachmentRootIds.Count == 0 && directTargetIds.Count > 0)
        {
            detachmentRootIds = await (
                from parent in db.Units.AsNoTracking()
                join child in db.Units.AsNoTracking() on parent.Id equals child.ParentId
                where directTargetIds.Contains(parent.Id)
                   && parent.EntryType == "upgrade"
                   && parent.ParentId == null
                   && child.EntryType == "selectionEntryGroup"
                select parent.Id
            ).Distinct().ToListAsync();
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
    /// Returns a flat list of unit–detachment associations for the given fraction.
    /// Each item identifies a unit and the set of detachment IDs that are required to
    /// unlock it, as encoded in the roster-scope <c>DetachmentConditions</c> of the
    /// catalogue-level entry links.
    /// </summary>
    [HttpGet("{id}/detachment-conditions")]
    public async Task<ActionResult<IEnumerable<BsDataUnitDetachmentConditions>>> GetDetachmentConditions(string id)
    {
        if (!await db.Catalogues.AnyAsync(c => c.Id == id && !c.Library))
            return NotFound();

        var catalogueIds = await CollectCatalogueIdsAsync(id);

        var links = await db.CatalogueLevelEntryLinks
            .AsNoTracking()
            .Where(l => catalogueIds.Contains(l.CatalogueId) && l.DetachmentConditions != null)
            .ToListAsync();

        var result = links
            .Select(l => new { l.TargetId, DetachmentIds = ExtractDetachmentIds(l.DetachmentConditions) })
            .Where(x => x.DetachmentIds.Count > 0)
            .GroupBy(x => x.TargetId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BsDataUnitDetachmentConditions
            {
                UnitId = g.Key,
                DetachmentIds = g.SelectMany(x => x.DetachmentIds)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList(),
            })
            .ToList();

        return Ok(result);
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
    private static string? ExtractRequiredDetachmentId(ICollection<BsDataModifierGroup> modifierGroups) =>
        ExtractRequiredDetachmentIdCore(modifierGroups.Select(g => (g.Modifiers, g.Conditions)));

    private static string? ExtractRequiredDetachmentIdCore(IEnumerable<(string? Modifiers, string? Conditions)> groups)
    {
        foreach (var (modifiers, conditions) in groups)
        {
            if (modifiers is null || conditions is null)
                continue;

            try
            {
                using var modDoc = JsonDocument.Parse(modifiers);
                var setsHiddenTrue = modDoc.RootElement.EnumerateArray().Any(m =>
                    m.TryGetProperty("field", out var f) &&
                    f.ValueKind == JsonValueKind.String &&
                    string.Equals(f.GetString(), "hidden", StringComparison.OrdinalIgnoreCase) &&
                    m.TryGetProperty("value", out var v) &&
                    v.ValueKind == JsonValueKind.String &&
                    string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase));

                if (!setsHiddenTrue)
                    continue;

                using var condDoc = JsonDocument.Parse(conditions);
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
    /// Parses a <c>DetachmentConditions</c> JSON array and returns every distinct
    /// <c>childId</c> found in it.  Returns an empty list when <paramref name="conditions"/>
    /// is <see langword="null"/> or contains no valid <c>childId</c> entries.
    /// </summary>
    private static List<string> ExtractDetachmentIds(string? conditions)
    {
        if (conditions is null)
            return [];

        var ids = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(conditions);
            foreach (var cond in doc.RootElement.EnumerateArray())
            {
                if (cond.TryGetProperty("childId", out var childId) &&
                    childId.ValueKind == JsonValueKind.String &&
                    childId.GetString() is { Length: > 0 } detachmentId)
                    ids.Add(detachmentId);
            }
        }
        catch (JsonException)
        {
            // Skip malformed JSON — should not happen with well-formed import data.
        }

        return ids;
    }

    /// <summary>
    /// Collects all catalogue IDs reachable from <paramref name="rootId"/>
    /// by following catalogueLinks recursively.
    /// Loads all catalogue links in a single query then traverses them in memory.
    /// </summary>
    /// <param name="rootId">The starting catalogue ID.</param>
    /// <param name="importRootEntriesOnly">
    /// When <c>true</c>, follows links where <c>importRootEntries=true</c> AND links
    /// whose target catalogue has <c>library=true</c> (internal shared library
    /// dependencies are always "own" regardless of the importRootEntries flag).
    /// Full faction catalogues linked without importRootEntries are excluded
    /// (e.g. Chaos Space Marines linked from Chaos Daemons).
    /// When <c>false</c>, all catalogue links are followed.
    /// </param>
    private async Task<HashSet<string>> CollectCatalogueIdsAsync(string rootId, bool importRootEntriesOnly = false)
    {
        // Load all links once to avoid N+1 queries during traversal.
        var allLinks = await db.CatalogueLinks.AsNoTracking()
            .Select(l => new { l.CatalogueId, l.TargetId, l.ImportRootEntries })
            .ToListAsync();

        // When importRootEntriesOnly is true, also allow links to library catalogues
        // (library=true) so that internal shared dependencies are always included as
        // "own" even when BSData omits the importRootEntries flag on the link
        // (e.g. Chaos - Daemons Library linked from Death Guard without importRootEntries).
        var libraryCatalogueIds = importRootEntriesOnly
            ? (await db.Catalogues.AsNoTracking()
                .Where(c => c.Library)
                .Select(c => c.Id)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var linkMap = allLinks
            .Where(l => !importRootEntriesOnly || l.ImportRootEntries || libraryCatalogueIds.Contains(l.TargetId))
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

    /// <summary>
    /// Determines which library catalogues reachable from <paramref name="factionId"/> are
    /// "Allied" rather than "own" — meaning they primarily belong to a different faction.
    /// Two rules are applied:
    /// <list type="bullet">
    ///   <item><b>Rule 1 (primary-faction signal)</b>: a library L is Allied if its units
    ///   carry <c>notInstanceOf(scope=primary-catalogue, childId=G)</c> modifiers where G ≠ F,
    ///   and none of those units carry <c>notInstanceOf(..., childId=F)</c>.</item>
    ///   <item><b>Rule 2 (library-based faction signal)</b>: a library L is Allied if faction F
    ///   has its own root units (F is not library-based) and at least one library-based faction
    ///   (a faction with no own root units) also links to L via <c>importRootEntries=true</c>.</item>
    /// </list>
    /// </summary>
    private async Task<HashSet<string>> DetermineAlliedLibraryCatalogueIdsAsync(string factionId)
    {
        var ownIds = await CollectCatalogueIdsAsync(factionId, importRootEntriesOnly: true);
        ownIds.Remove(factionId); // the faction's own catalogue is never Allied

        // Narrow to library catalogues only.
        var libraryIds = await db.Catalogues.AsNoTracking()
            .Where(c => c.Library && ownIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        if (libraryIds.Count == 0)
            return [];

        var alliedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Rule 1: primary-faction signal ──────────────────────────────────────
        // Collect modifier groups for units in the library catalogues whose
        // modifiers set hidden=true (the "Allied visibility" pattern).
        var unitLookup = await db.Units.AsNoTracking()
            .Where(u => libraryIds.Contains(u.CatalogueId))
            .Select(u => new { u.Id, u.CatalogueId })
            .ToListAsync();

        var unitIdToCatalogueId = unitLookup
            .ToDictionary(u => u.Id, u => u.CatalogueId, StringComparer.OrdinalIgnoreCase);

        var unitIds = unitIdToCatalogueId.Keys.ToList();

        var modifierGroups = await db.ModifierGroups.AsNoTracking()
            .Where(mg => unitIds.Contains(mg.UnitId) && mg.Modifiers != null && mg.Conditions != null)
            .Select(mg => new { mg.UnitId, mg.Modifiers, mg.Conditions })
            .ToListAsync();

        // Build a set of primary-faction childIds per library catalogue.
        var primaryFactionIdsByLib = libraryIds
            .ToDictionary(id => id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        foreach (var mg in modifierGroups)
        {
            if (!unitIdToCatalogueId.TryGetValue(mg.UnitId, out var catId))
                continue;
            if (!ModifierGroupSetsHiddenTrue(mg.Modifiers!))
                continue;
            foreach (var childId in GetNotInstanceOfPrimaryCatalogueChildIds(mg.Conditions!))
                primaryFactionIdsByLib[catId].Add(childId);
        }

        foreach (var (libId, primaryIds) in primaryFactionIdsByLib)
        {
            // Allied: the library signals a primary faction that is NOT factionId.
            if (primaryIds.Count > 0 && !primaryIds.Contains(factionId))
                alliedIds.Add(libId);
        }

        // ── Rule 2: library-based faction signal ────────────────────────────────
        // A faction is "library-based" when it has no own root units (parentId=null)
        // in its own catalogue; all its units come from linked libraries (e.g. CK, IK).
        var factionHasOwnRootUnits = await db.Units.AsNoTracking()
            .AnyAsync(u => u.CatalogueId == factionId && u.ParentId == null);

        if (factionHasOwnRootUnits)
        {
            var allFactionIds = await db.Catalogues.AsNoTracking()
                .Where(c => !c.Library)
                .Select(c => c.Id)
                .ToListAsync();

            var factionsWithOwnRootUnits = await db.Units.AsNoTracking()
                .Where(u => allFactionIds.Contains(u.CatalogueId) && u.ParentId == null)
                .Select(u => u.CatalogueId)
                .Distinct()
                .ToListAsync();

            var libraryBasedFactionIds = allFactionIds
                .Except(factionsWithOwnRootUnits, StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            libraryBasedFactionIds.Remove(factionId);

            if (libraryBasedFactionIds.Count > 0)
            {
                var libLinkedByLibraryBasedFactions = await db.CatalogueLinks.AsNoTracking()
                    .Where(l => libraryBasedFactionIds.Contains(l.CatalogueId)
                                && l.ImportRootEntries
                                && libraryIds.Contains(l.TargetId))
                    .Select(l => l.TargetId)
                    .Distinct()
                    .ToListAsync();

                foreach (var libId in libLinkedByLibraryBasedFactions)
                    alliedIds.Add(libId);
            }
        }

        return alliedIds;
    }

    /// <summary>
    /// Returns <c>true</c> when the modifier group JSON array contains an entry
    /// with <c>field="hidden"</c> and <c>value="true"</c>.
    /// </summary>
    private static bool ModifierGroupSetsHiddenTrue(string modifiers)
    {
        try
        {
            using var doc = JsonDocument.Parse(modifiers);
            return doc.RootElement.EnumerateArray().Any(m =>
                m.TryGetProperty("field", out var f) &&
                f.ValueKind == JsonValueKind.String &&
                string.Equals(f.GetString(), "hidden", StringComparison.OrdinalIgnoreCase) &&
                m.TryGetProperty("value", out var v) &&
                v.ValueKind == JsonValueKind.String &&
                string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a condition group JSON array and returns all <c>childId</c> values for
    /// conditions with <c>type="notInstanceOf"</c> and <c>scope="primary-catalogue"</c>.
    /// </summary>
    private static IEnumerable<string> GetNotInstanceOfPrimaryCatalogueChildIds(string conditions)
    {
        var ids = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(conditions);
            foreach (var cond in doc.RootElement.EnumerateArray())
            {
                if (!cond.TryGetProperty("type", out var typeProp) ||
                    !string.Equals(typeProp.GetString(), "notInstanceOf", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!cond.TryGetProperty("scope", out var scopeProp) ||
                    !string.Equals(scopeProp.GetString(), "primary-catalogue", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cond.TryGetProperty("childId", out var childId) &&
                    childId.ValueKind == JsonValueKind.String &&
                    childId.GetString() is { Length: > 0 } id)
                    ids.Add(id);
            }
        }
        catch (JsonException)
        {
            // Skip malformed JSON — should not happen with well-formed import data.
        }
        return ids;
    }
}
