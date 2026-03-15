using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Xml.Linq;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Services;

public class BsDataImportService(BsDataDbContext db, IHttpClientFactory httpClientFactory, ILogger<BsDataImportService> logger)
{
    private const string GithubApiBase = "https://api.github.com/repos/BSData/wh40k-10e/contents";
    private const string GithubRawBase = "https://raw.githubusercontent.com/BSData/wh40k-10e/main/";
    private static readonly XNamespace Ns = "http://www.battlescribe.net/schema/catalogueSchema";
    private const string ConditionTypeGreaterThan = "greaterThan";

    public async Task<int> ImportAsync()
    {
        var client = httpClientFactory.CreateClient("github");

        // 1. Fetch list of .cat files from the repository
        var catFiles = await FetchCatFileListAsync(client);
        logger.LogInformation("Found {Count} .cat files to import", catFiles.Count);

        // 2. Drop and recreate all BSData tables so schema changes take effect
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        int totalUnits = 0;
        int totalProfiles = 0;
        int totalCategories = 0;
        int totalInfoLinks = 0;
        int totalEntryLinks = 0;
        int totalConstraints = 0;
        int totalModifierGroups = 0;
        int totalDetachmentVisibilities = 0;
        int totalCostTiers = 0;
        int totalRules = 0;
        int totalCatalogueLinks = 0;
        int totalCatalogueLevelEntryLinks = 0;

        // Keep global seen sets to avoid adding entities with duplicate primary keys
        var seenCatalogueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenInfoLinkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEntryLinkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenConstraintIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCatalogueLinkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 3. Process each .cat file
        foreach (var (fileName, downloadUrl) in catFiles)
        {
            try
            {
                logger.LogInformation("Importing {File}", fileName);
                var xml = await client.GetStringAsync(downloadUrl);
                var (catalogue, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, rules, catalogueLinks, catalogueLevelEntryLinks, detachmentVisibilities) = ParseCatalogueXml(xml);

                if (catalogue == null) continue;

                if (!seenCatalogueIds.Add(catalogue.Id))
                {
                    logger.LogWarning("Skipping catalogue {Id} from {File} — duplicate id", catalogue.Id, fileName);
                }
                else
                {
                    db.Catalogues.Add(catalogue);
                }

                var newUnits = new List<BsDataUnit>();
                foreach (var u in units)
                {
                    if (string.IsNullOrEmpty(u.Id)) continue;
                    if (!seenUnitIds.Add(u.Id))
                    {
                        logger.LogDebug("Skipping duplicate unit {UnitId} from {File}", u.Id, fileName);
                        continue;
                    }
                    newUnits.Add(u);
                }

                var newProfiles = new List<BsDataProfile>();
                foreach (var p in profiles)
                {
                    if (string.IsNullOrEmpty(p.Id)) continue;
                    if (!seenProfileIds.Add(p.Id))
                    {
                        logger.LogDebug("Skipping duplicate profile {ProfileId} from {File}", p.Id, fileName);
                        continue;
                    }
                    newProfiles.Add(p);
                }

                // Only include categories, infoLinks and entryLinks for units that were accepted
                var acceptedUnitIds = new HashSet<string>(newUnits.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
                var newCategories = categories.Where(c => acceptedUnitIds.Contains(c.UnitId)).ToList();

                var newInfoLinks = new List<BsDataInfoLink>();
                foreach (var il in infoLinks.Where(l => acceptedUnitIds.Contains(l.UnitId)))
                {
                    if (!seenInfoLinkIds.Add(il.Id))
                    {
                        logger.LogDebug("Skipping duplicate infoLink {InfoLinkId} from {File}", il.Id, fileName);
                        continue;
                    }
                    newInfoLinks.Add(il);
                }

                var newEntryLinks = new List<BsDataEntryLink>();
                foreach (var el in entryLinks.Where(l => acceptedUnitIds.Contains(l.UnitId)))
                {
                    if (!seenEntryLinkIds.Add(el.Id))
                    {
                        logger.LogDebug("Skipping duplicate entryLink {EntryLinkId} from {File}", el.Id, fileName);
                        continue;
                    }
                    newEntryLinks.Add(el);
                }

                var newConstraints = new List<BsDataConstraint>();
                foreach (var c in constraints.Where(c => acceptedUnitIds.Contains(c.UnitId)))
                {
                    if (!seenConstraintIds.Add(c.UnitId + ":" + c.Id))
                    {
                        logger.LogDebug("Skipping duplicate constraint {UnitId}:{ConstraintId} from {File}", c.UnitId, c.Id, fileName);
                        continue;
                    }
                    newConstraints.Add(c);
                }

                // ModifierGroups use a DB-generated int PK so no seen-set needed;
                // filter only for accepted units.
                var newModifierGroups = modifierGroups.Where(g => acceptedUnitIds.Contains(g.UnitId)).ToList();

                // DetachmentVisibilities also use a DB-generated int PK; filter for accepted units.
                var newDetachmentVisibilities = detachmentVisibilities.Where(v => acceptedUnitIds.Contains(v.UnitId)).ToList();

                // CostTiers also use a DB-generated int PK; filter for accepted units.
                var newCostTiers = costTiers.Where(t => acceptedUnitIds.Contains(t.UnitId)).ToList();

                var newRules = new List<BsDataRule>();
                foreach (var r in rules)
                {
                    if (string.IsNullOrEmpty(r.Id)) continue;
                    if (!seenRuleIds.Add(r.Id))
                    {
                        logger.LogDebug("Skipping duplicate rule {RuleId} from {File}", r.Id, fileName);
                        continue;
                    }
                    newRules.Add(r);
                }

                var newCatalogueLinks = new List<BsDataCatalogueLink>();
                foreach (var cl in catalogueLinks)
                {
                    if (string.IsNullOrEmpty(cl.Id)) continue;
                    if (!seenCatalogueLinkIds.Add(cl.Id))
                    {
                        logger.LogDebug("Skipping duplicate catalogueLink {LinkId} from {File}", cl.Id, fileName);
                        continue;
                    }
                    newCatalogueLinks.Add(cl);
                }

                if (newUnits.Count > 0)
                    db.Units.AddRange(newUnits);

                if (newProfiles.Count > 0)
                    db.Profiles.AddRange(newProfiles);

                if (newCategories.Count > 0)
                    db.UnitCategories.AddRange(newCategories);

                if (newInfoLinks.Count > 0)
                    db.InfoLinks.AddRange(newInfoLinks);

                if (newEntryLinks.Count > 0)
                    db.EntryLinks.AddRange(newEntryLinks);

                if (newConstraints.Count > 0)
                    db.Constraints.AddRange(newConstraints);

                if (newModifierGroups.Count > 0)
                    db.ModifierGroups.AddRange(newModifierGroups);

                if (newDetachmentVisibilities.Count > 0)
                    db.DetachmentVisibilities.AddRange(newDetachmentVisibilities);

                if (newCostTiers.Count > 0)
                    db.CostTiers.AddRange(newCostTiers);

                if (newRules.Count > 0)
                    db.Rules.AddRange(newRules);

                if (newCatalogueLinks.Count > 0)
                    db.CatalogueLinks.AddRange(newCatalogueLinks);

                // Catalogue-level entry links use auto-generated int PKs so no seen-set
                // is needed beyond the per-catalogue deduplication in ParseCatalogueXml.
                if (catalogueLevelEntryLinks.Count > 0)
                    db.CatalogueLevelEntryLinks.AddRange(catalogueLevelEntryLinks);

                await db.SaveChangesAsync();
                totalUnits += newUnits.Count;
                totalProfiles += newProfiles.Count;
                totalCategories += newCategories.Count;
                totalInfoLinks += newInfoLinks.Count;
                totalEntryLinks += newEntryLinks.Count;
                totalConstraints += newConstraints.Count;
                totalModifierGroups += newModifierGroups.Count;
                totalDetachmentVisibilities += newDetachmentVisibilities.Count;
                totalCostTiers += newCostTiers.Count;
                totalRules += newRules.Count;
                totalCatalogueLinks += newCatalogueLinks.Count;
                totalCatalogueLevelEntryLinks += catalogueLevelEntryLinks.Count;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import {File}", fileName);
                db.ChangeTracker.Clear();
            }
        }

        logger.LogInformation(
            "Import complete. Units: {TotalUnits}, Profiles: {TotalProfiles}, Categories: {TotalCategories}, " +
            "InfoLinks: {TotalInfoLinks}, EntryLinks: {TotalEntryLinks}, Constraints: {TotalConstraints}, " +
            "ModifierGroups: {TotalModifierGroups}, DetachmentVisibilities: {TotalDetachmentVisibilities}, " +
            "CostTiers: {TotalCostTiers}, Rules: {TotalRules}, " +
            "CatalogueLinks: {TotalCatalogueLinks}, CatalogueLevelEntryLinks: {TotalCatalogueLevelEntryLinks}",
            totalUnits, totalProfiles, totalCategories, totalInfoLinks, totalEntryLinks,
            totalConstraints, totalModifierGroups, totalDetachmentVisibilities, totalCostTiers,
            totalRules, totalCatalogueLinks, totalCatalogueLevelEntryLinks);
        return totalUnits;
    }

    private async Task<List<(string Name, string DownloadUrl)>> FetchCatFileListAsync(HttpClient client)
    {
        var response = await client.GetAsync(GithubApiBase);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase}. " +
                "Possible rate limit – retry later or set a GitHub token.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var files = new List<(string, string)>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith(".cat", StringComparison.OrdinalIgnoreCase))
                continue;

            var downloadUrl = item.TryGetProperty("download_url", out var dlProp)
                ? dlProp.GetString() ?? (GithubRawBase + Uri.EscapeDataString(name))
                : GithubRawBase + Uri.EscapeDataString(name);

            files.Add((name, downloadUrl));
        }

        return files;
    }

    private static (
        BsDataCatalogue? Catalogue,
        List<BsDataUnit> Units,
        List<BsDataProfile> Profiles,
        List<BsDataUnitCategory> Categories,
        List<BsDataInfoLink> InfoLinks,
        List<BsDataEntryLink> EntryLinks,
        List<BsDataConstraint> Constraints,
        List<BsDataModifierGroup> ModifierGroups,
        List<BsDataCostTier> CostTiers,
        List<BsDataRule> Rules,
        List<BsDataCatalogueLink> CatalogueLinks,
        List<BsDataCatalogueEntryLink> CatalogueLevelEntryLinks,
        List<BsDataDetachmentVisibility> DetachmentVisibilities)
        ParseCatalogueXml(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return (null, [], [], [], [], [], [], [], [], [], [], [], []); }

        var root = doc.Root;
        if (root == null) return (null, [], [], [], [], [], [], [], [], [], [], [], []);

        var id = root.Attribute("id")?.Value ?? "";
        var name = root.Attribute("name")?.Value ?? "";
        if (!int.TryParse(root.Attribute("revision")?.Value, out var revision))
            revision = 0;
        var library = string.Equals(root.Attribute("library")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var battleScribeVersion = root.Attribute("battleScribeVersion")?.Value;
        var gameSystemId = root.Attribute("gameSystemId")?.Value;
        var gameSystemRevision = int.TryParse(root.Attribute("gameSystemRevision")?.Value, out var gsRev) ? gsRev : (int?)null;
        var authorName = root.Attribute("authorName")?.Value;
        var authorContact = root.Attribute("authorContact")?.Value;
        var authorUrl = root.Attribute("authorUrl")?.Value;

        var catalogue = new BsDataCatalogue
        {
            Id = id,
            Name = name,
            Revision = revision,
            Library = library,
            BattleScribeVersion = battleScribeVersion,
            GameSystemId = gameSystemId,
            GameSystemRevision = gameSystemRevision,
            AuthorName = authorName,
            AuthorContact = authorContact,
            AuthorUrl = authorUrl,
            FetchedAt = DateTime.UtcNow,
        };

        var units = new List<BsDataUnit>();
        var profiles = new List<BsDataProfile>();
        var categories = new List<BsDataUnitCategory>();
        var infoLinks = new List<BsDataInfoLink>();
        var entryLinks = new List<BsDataEntryLink>();
        var constraints = new List<BsDataConstraint>();
        var modifierGroups = new List<BsDataModifierGroup>();
        var costTiers = new List<BsDataCostTier>();
        var rules = new List<BsDataRule>();
        var catalogueLinks = new List<BsDataCatalogueLink>();
        var catalogueLevelEntryLinks = new List<BsDataCatalogueEntryLink>();
        var detachmentVisibilities = new List<BsDataDetachmentVisibility>();
        var seenUnitIds = new HashSet<string>();

        // Parse sharedSelectionEntries (top-level reusable units/models)
        foreach (var entry in root
            .Element(Ns + "sharedSelectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(entry, id, null, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, detachmentVisibilities, seenUnitIds);
        }

        // Parse sharedSelectionEntryGroups (top-level reusable option groups)
        foreach (var group in root
            .Element(Ns + "sharedSelectionEntryGroups")
            ?.Elements(Ns + "selectionEntryGroup") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(group, id, null, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, detachmentVisibilities, seenUnitIds);
        }

        // Also parse top-level selectionEntries (force org slots, etc.)
        foreach (var entry in root
            .Element(Ns + "selectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(entry, id, null, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, detachmentVisibilities, seenUnitIds);
        }

        // Parse sharedRules (catalogue-level rules/abilities)
        foreach (var rule in root
            .Element(Ns + "sharedRules")
            ?.Elements(Ns + "rule") ?? Enumerable.Empty<XElement>())
        {
            ExtractRule(rule, id, rules);
        }

        // Also parse top-level rules
        foreach (var rule in root
            .Element(Ns + "rules")
            ?.Elements(Ns + "rule") ?? Enumerable.Empty<XElement>())
        {
            ExtractRule(rule, id, rules);
        }

        // Parse catalogueLinks (dependencies on other catalogues)
        foreach (var link in root
            .Element(Ns + "catalogueLinks")
            ?.Elements(Ns + "catalogueLink") ?? Enumerable.Empty<XElement>())
        {
            var linkId = link.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(linkId)) continue;
            var targetId = link.Attribute("targetId")?.Value ?? "";
            if (string.IsNullOrEmpty(targetId)) continue;
            catalogueLinks.Add(new BsDataCatalogueLink
            {
                Id = linkId,
                CatalogueId = id,
                Name = link.Attribute("name")?.Value ?? "",
                TargetId = targetId,
                Type = link.Attribute("type")?.Value ?? "",
                ImportRootEntries = string.Equals(link.Attribute("importRootEntries")?.Value, "true", StringComparison.OrdinalIgnoreCase),
            });
        }

        // Parse catalogue-level entryLinks — links declared directly under the
        // catalogue root (not inside a selectionEntry).  These record which shared
        // entries (e.g. the Detachment root) a faction explicitly imports and are
        // used by GetDetachments to resolve the correct detachment root when it
        // lives in a library catalogue rather than in the faction's own catalogue.
        var seenCatalogueEntryLinkTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root
            .Element(Ns + "entryLinks")
            ?.Elements(Ns + "entryLink") ?? Enumerable.Empty<XElement>())
        {
            var targetId = el.Attribute("targetId")?.Value;
            if (string.IsNullOrEmpty(targetId)) continue;
            if (!seenCatalogueEntryLinkTargets.Add(targetId)) continue;
            catalogueLevelEntryLinks.Add(new BsDataCatalogueEntryLink
            {
                CatalogueId = id,
                TargetId = targetId,
            });
        }

        return (catalogue, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, rules, catalogueLinks, catalogueLevelEntryLinks, detachmentVisibilities);
    }

    private static void ExtractRule(XElement rule, string catalogueId, List<BsDataRule> rules)
    {
        var ruleId = rule.Attribute("id")?.Value ?? "";
        if (string.IsNullOrEmpty(ruleId)) return;

        rules.Add(new BsDataRule
        {
            Id = ruleId,
            CatalogueId = catalogueId,
            Name = rule.Attribute("name")?.Value ?? "",
            Description = rule.Element(Ns + "description")?.Value,
            Hidden = string.Equals(rule.Attribute("hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase),
            PublicationId = rule.Attribute("publicationId")?.Value,
            Page = rule.Attribute("page")?.Value,
        });
    }

    private static void ExtractEntry(
        XElement entry,
        string catalogueId,
        string? parentId,
        List<BsDataUnit> units,
        List<BsDataProfile> profiles,
        List<BsDataUnitCategory> categories,
        List<BsDataInfoLink> infoLinks,
        List<BsDataEntryLink> entryLinks,
        List<BsDataConstraint> constraints,
        List<BsDataModifierGroup> modifierGroups,
        List<BsDataCostTier> costTiers,
        List<BsDataDetachmentVisibility> detachmentVisibilities,
        HashSet<string> seenUnitIds,
        int depth = 0)
    {
        if (depth > 10) return;
        var unitId = entry.Attribute("id")?.Value ?? "";
        if (string.IsNullOrEmpty(unitId) || seenUnitIds.Contains(unitId)) return;
        seenUnitIds.Add(unitId);

        var unitName = entry.Attribute("name")?.Value ?? "";
        var entryType = entry.Attribute("type")?.Value;
        // <selectionEntryGroup> elements carry no "type" XML attribute; label them explicitly
        // so that API consumers can distinguish them from regular selectionEntry nodes.
        if (entryType is null && entry.Name.LocalName == "selectionEntryGroup")
            entryType = "selectionEntryGroup";
        var hidden = string.Equals(entry.Attribute("hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var collective = string.Equals(entry.Attribute("collective")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var import = string.Equals(entry.Attribute("import")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var publicationId = entry.Attribute("publicationId")?.Value;
        var page = entry.Attribute("page")?.Value;

        // Get points cost
        decimal? points = null;
        var pointsValue = entry
            .Element(Ns + "costs")
            ?.Elements(Ns + "cost")
            .Where(c => (c.Attribute("name")?.Value ?? "").Contains("pts", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();
        if (decimal.TryParse(pointsValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedPoints))
            points = parsedPoints;

        // Extract min/max roster quantities from top-level constraints
        int? minInRoster = null;
        int? maxInRoster = null;
        foreach (var constraint in entry.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
        {
            var cType = constraint.Attribute("type")?.Value;
            var cField = constraint.Attribute("field")?.Value;
            if (!string.Equals(cField, "selections", StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(constraint.Attribute("value")?.Value, out var cVal))
            {
                if (cType == "min" && minInRoster == null)
                    minInRoster = cVal;
                else if (cType == "max" && maxInRoster == null)
                    maxInRoster = cVal;
            }
        }

        units.Add(new BsDataUnit
        {
            Id = unitId,
            CatalogueId = catalogueId,
            ParentId = parentId,
            Name = unitName,
            EntryType = entryType,
            Points = points,
            Hidden = hidden,
            Collective = collective,
            Import = import,
            PublicationId = publicationId,
            Page = page,
            MinInRoster = minInRoster,
            MaxInRoster = maxInRoster,
        });

        // Extract categoryLinks (faction, role, keywords)
        foreach (var cl in entry
            .Element(Ns + "categoryLinks")
            ?.Elements(Ns + "categoryLink") ?? Enumerable.Empty<XElement>())
        {
            var catName = cl.Attribute("name")?.Value ?? "";
            if (string.IsNullOrEmpty(catName)) continue;
            var primary = string.Equals(cl.Attribute("primary")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            categories.Add(new BsDataUnitCategory
            {
                UnitId = unitId,
                Name = catName,
                Primary = primary,
            });
        }

        // Extract profiles
        foreach (var profile in entry
            .Element(Ns + "profiles")
            ?.Elements(Ns + "profile") ?? Enumerable.Empty<XElement>())
        {
            var profileId = profile.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(profileId)) continue;

            var profileName = profile.Attribute("name")?.Value ?? "";
            var typeName = profile.Attribute("typeName")?.Value;

            var characteristics = profile
                .Element(Ns + "characteristics")
                ?.Elements(Ns + "characteristic")
                .ToDictionary(
                    c => c.Attribute("name")?.Value ?? "",
                    c => c.Value);

            profiles.Add(new BsDataProfile
            {
                Id = profileId,
                UnitId = unitId,
                Name = profileName,
                TypeName = typeName,
                Characteristics = characteristics != null
                    ? JsonSerializer.Serialize(characteristics)
                    : null,
            });
        }

        // Extract infoLinks
        foreach (var il in entry
            .Element(Ns + "infoLinks")
            ?.Elements(Ns + "infoLink") ?? Enumerable.Empty<XElement>())
        {
            var linkId = il.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(linkId)) continue;
            var targetId = il.Attribute("targetId")?.Value ?? "";
            if (string.IsNullOrEmpty(targetId)) continue;
            infoLinks.Add(new BsDataInfoLink
            {
                Id = linkId,
                UnitId = unitId,
                Name = il.Attribute("name")?.Value ?? "",
                Hidden = string.Equals(il.Attribute("hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                TargetId = targetId,
                Type = il.Attribute("type")?.Value,
            });
        }

        // Extract entryLinks
        foreach (var el in entry
            .Element(Ns + "entryLinks")
            ?.Elements(Ns + "entryLink") ?? Enumerable.Empty<XElement>())
        {
            var linkId = el.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(linkId)) continue;
            var targetId = el.Attribute("targetId")?.Value ?? "";
            if (string.IsNullOrEmpty(targetId)) continue;

            // Detect detachment-dependency pattern:
            // <modifier type="set" value="true" field="hidden">
            //   <conditionGroups><conditionGroup type="and"><conditions>
            //     <condition scope="roster" field="selections" type="lessThan" value="1" childId="<DETACHMENT-ID>"/>
            //   </conditions></conditionGroup></conditionGroups>
            // </modifier>
            // When found, store the inverted unlock modifier/condition so GetUnitsTree can
            // present the unit as hidden-by-default with a detachment-unlock modifierGroup.
            var (detachmentModifiers, detachmentConditions) = ParseEntryLinkDetachmentDependency(el);

            entryLinks.Add(new BsDataEntryLink
            {
                Id = linkId,
                UnitId = unitId,
                Name = el.Attribute("name")?.Value ?? "",
                Hidden = string.Equals(el.Attribute("hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                TargetId = targetId,
                Type = el.Attribute("type")?.Value,
                DetachmentModifiers = detachmentModifiers,
                DetachmentConditions = detachmentConditions,
            });
        }

        // Extract constraints
        foreach (var c in entry.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
        {
            var constraintId = c.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(constraintId)) continue;
            constraints.Add(new BsDataConstraint
            {
                Id = constraintId,
                UnitId = unitId,
                Field = c.Attribute("field")?.Value,
                Scope = c.Attribute("scope")?.Value,
                Value = c.Attribute("value")?.Value,
                PercentValue = string.Equals(c.Attribute("percentValue")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                Shared = string.Equals(c.Attribute("shared")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                IncludeChildSelections = string.Equals(c.Attribute("includeChildSelections")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                IncludeChildForces = string.Equals(c.Attribute("includeChildForces")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                ChildId = c.Attribute("childId")?.Value,
                Type = c.Attribute("type")?.Value,
            });
        }

        // Extract modifierGroups
        foreach (var mg in entry
            .Element(Ns + "modifierGroups")
            ?.Elements(Ns + "modifierGroup") ?? Enumerable.Empty<XElement>())
        {
            var modifiers = mg
                .Element(Ns + "modifiers")
                ?.Elements(Ns + "modifier")
                .Select(m => new
                {
                    id = m.Attribute("id")?.Value,
                    field = m.Attribute("field")?.Value,
                    type = m.Attribute("type")?.Value,
                    value = m.Attribute("value")?.Value,
                })
                .ToList();

            var conditions = mg
                .Element(Ns + "conditions")
                ?.Elements(Ns + "condition")
                .Select(c => new
                {
                    id = c.Attribute("id")?.Value,
                    field = c.Attribute("field")?.Value,
                    scope = c.Attribute("scope")?.Value,
                    value = c.Attribute("value")?.Value,
                    type = c.Attribute("type")?.Value,
                    childId = c.Attribute("childId")?.Value,
                })
                .ToList();

            modifierGroups.Add(new BsDataModifierGroup
            {
                UnitId = unitId,
                Modifiers = modifiers is { Count: > 0 } ? JsonSerializer.Serialize(modifiers) : null,
                Conditions = conditions is { Count: > 0 } ? JsonSerializer.Serialize(conditions) : null,
            });
        }

        // Extract cost tiers from direct <modifiers> (not inside <modifierGroups>).
        // Units with multiple cost tiers have <modifier type="set" field="{pts_typeId}" value="...">
        // with a condition on the number of models (field="selections" childId="model").
        // Also extract hidden-visibility conditions used to filter detachment entries by faction
        // (see ParseDetachmentVisibilities for details).
        detachmentVisibilities.AddRange(ParseDetachmentVisibilities(entry, unitId));

        var ptsTypeId = entry
            .Element(Ns + "costs")
            ?.Elements(Ns + "cost")
            .FirstOrDefault(c => (c.Attribute("name")?.Value ?? "").Contains("pts", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("typeId")?.Value;

        if (points.HasValue && ptsTypeId != null)
        {
            var costMods = new List<(decimal NewCost, string CondType, int CondValue)>();

            foreach (var mod in entry.Element(Ns + "modifiers")?.Elements(Ns + "modifier") ?? Enumerable.Empty<XElement>())
            {
                if (mod.Attribute("type")?.Value != "set") continue;
                if (mod.Attribute("field")?.Value != ptsTypeId) continue;
                if (!decimal.TryParse(mod.Attribute("value")?.Value,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var newCost)) continue;

                var cond = mod.Element(Ns + "conditions")
                    ?.Elements(Ns + "condition")
                    .FirstOrDefault(c =>
                        c.Attribute("field")?.Value == "selections" &&
                        c.Attribute("childId")?.Value == "model");
                if (cond == null) continue;

                var condType = cond.Attribute("type")?.Value ?? "";
                if (!int.TryParse(cond.Attribute("value")?.Value, out var condValue)) continue;

                costMods.Add((newCost, condType, condValue));
            }

            if (costMods.Count > 0)
            {
                // Determine model min/max from selectionEntryGroups containing model entries.
                // Direct model entries (e.g. a champion) contribute a fixed count.
                int directModelMin = 0;
                foreach (var se in entry.Element(Ns + "selectionEntries")
                    ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
                {
                    if (se.Attribute("type")?.Value != "model") continue;
                    foreach (var c in se.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
                    {
                        if (c.Attribute("field")?.Value == "selections" &&
                            c.Attribute("type")?.Value == "min" &&
                            int.TryParse(c.Attribute("value")?.Value, out var v))
                            directModelMin += v;
                    }
                }

                int? groupModelMin = null;
                int? groupModelMax = null;
                foreach (var seg in entry.Element(Ns + "selectionEntryGroups")
                    ?.Elements(Ns + "selectionEntryGroup") ?? Enumerable.Empty<XElement>())
                {
                    bool hasModels = seg.Element(Ns + "selectionEntries")
                        ?.Elements(Ns + "selectionEntry")
                        .Any(se => se.Attribute("type")?.Value == "model") == true;
                    if (!hasModels) continue;

                    foreach (var c in seg.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
                    {
                        if (c.Attribute("field")?.Value != "selections") continue;
                        if (!int.TryParse(c.Attribute("value")?.Value, out var v)) continue;
                        if (c.Attribute("type")?.Value == "min")
                            groupModelMin = (groupModelMin ?? 0) + v;
                        if (c.Attribute("type")?.Value == "max")
                            groupModelMax = (groupModelMax ?? 0) + v;
                    }

                    // Fall back to model-entry max if no group-level max
                    if (!groupModelMax.HasValue)
                    {
                        foreach (var se in seg.Element(Ns + "selectionEntries")
                            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
                        {
                            if (se.Attribute("type")?.Value != "model") continue;
                            foreach (var c in se.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
                            {
                                if (c.Attribute("field")?.Value == "selections" &&
                                    c.Attribute("type")?.Value == "max" &&
                                    int.TryParse(c.Attribute("value")?.Value, out var v))
                                    groupModelMax = groupModelMax.HasValue ? Math.Max(groupModelMax.Value, v) : v;
                            }
                        }
                    }
                }

                int? modelMin;
                if (groupModelMin.HasValue)
                    modelMin = directModelMin + groupModelMin.Value;
                else if (directModelMin > 0)
                    modelMin = directModelMin;
                else
                    modelMin = null;
                int? modelMax = groupModelMax.HasValue ? directModelMin + groupModelMax.Value : null;

                // Sort modifiers by ascending threshold and build tiers
                costMods.Sort((a, b) => a.CondValue.CompareTo(b.CondValue));

                decimal tierPts = points.Value;
                int? tierMin = modelMin;

                for (int i = 0; i < costMods.Count; i++)
                {
                    var (newCost, condType, condValue) = costMods[i];
                    int threshold = condType == ConditionTypeGreaterThan ? condValue + 1 : condValue;

                    costTiers.Add(new BsDataCostTier
                    {
                        UnitId = unitId,
                        MinModels = tierMin,
                        MaxModels = threshold - 1,
                        Points = tierPts,
                    });

                    tierMin = threshold;
                    tierPts = newCost;
                }

                // Final tier
                costTiers.Add(new BsDataCostTier
                {
                    UnitId = unitId,
                    MinModels = tierMin,
                    MaxModels = modelMax,
                    Points = tierPts,
                });
            }
            else if (points.Value > 0)
            {
                // No cost-altering modifiers on the unit.  Units like Skitarii Rangers/Vanguard
                // have a fixed base cost and a fixed model count expressed entirely through a
                // selectionEntryGroup (no modifier conditions).  Emit a single cost tier so
                // that model-count information remains accessible via the cost-tiers endpoint.
                int directModelMin = 0;
                foreach (var se in entry.Element(Ns + "selectionEntries")
                    ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
                {
                    if (se.Attribute("type")?.Value != "model") continue;
                    foreach (var c in se.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
                    {
                        if (c.Attribute("field")?.Value == "selections" &&
                            c.Attribute("type")?.Value == "min" &&
                            int.TryParse(c.Attribute("value")?.Value, out var v))
                            directModelMin += v;
                    }
                }

                int? groupModelMin = null;
                int? groupModelMax = null;
                foreach (var seg in entry.Element(Ns + "selectionEntryGroups")
                    ?.Elements(Ns + "selectionEntryGroup") ?? Enumerable.Empty<XElement>())
                {
                    bool hasModels = seg.Element(Ns + "selectionEntries")
                        ?.Elements(Ns + "selectionEntry")
                        .Any(se => se.Attribute("type")?.Value == "model") == true;
                    if (!hasModels) continue;

                    foreach (var c in seg.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
                    {
                        if (c.Attribute("field")?.Value != "selections") continue;
                        if (!int.TryParse(c.Attribute("value")?.Value, out var v)) continue;
                        if (c.Attribute("type")?.Value == "min")
                            groupModelMin = (groupModelMin ?? 0) + v;
                        if (c.Attribute("type")?.Value == "max")
                            groupModelMax = (groupModelMax ?? 0) + v;
                    }
                }

                if (groupModelMin.HasValue || groupModelMax.HasValue)
                {
                    int? modelMin;
                    if (groupModelMin.HasValue)
                        modelMin = directModelMin + groupModelMin.Value;
                    else if (directModelMin > 0)
                        modelMin = directModelMin;
                    else
                        modelMin = null;
                    int? modelMax = groupModelMax.HasValue ? directModelMin + groupModelMax.Value : null;

                    costTiers.Add(new BsDataCostTier
                    {
                        UnitId = unitId,
                        MinModels = modelMin,
                        MaxModels = modelMax,
                        Points = points.Value,
                    });
                }
            }
        }

        // Recursively extract nested selectionEntries (models, wargear options within a unit)
        foreach (var nested in entry
            .Element(Ns + "selectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(nested, catalogueId, unitId, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, detachmentVisibilities, seenUnitIds, depth + 1);
        }

        // Recursively extract selectionEntryGroups (groups of options)
        foreach (var group in entry
            .Element(Ns + "selectionEntryGroups")
            ?.Elements(Ns + "selectionEntryGroup") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(group, catalogueId, unitId, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, costTiers, detachmentVisibilities, seenUnitIds, depth + 1);
        }
    }

    /// <summary>
    /// Parses direct <c>&lt;modifiers&gt;/&lt;modifier type="set" field="hidden" value="true"&gt;</c>
    /// elements on a selection entry and returns any <c>scope="primary-catalogue"</c> conditions
    /// as <see cref="BsDataDetachmentVisibility"/> records.
    /// </summary>
    private static IEnumerable<BsDataDetachmentVisibility> ParseDetachmentVisibilities(XElement entry, string unitId)
    {
        foreach (var mod in entry.Element(Ns + "modifiers")?.Elements(Ns + "modifier") ?? Enumerable.Empty<XElement>())
        {
            if (!string.Equals(mod.Attribute("type")?.Value, "set", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(mod.Attribute("field")?.Value, "hidden", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(mod.Attribute("value")?.Value, "true", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var cond in mod.Element(Ns + "conditions")?.Elements(Ns + "condition") ?? Enumerable.Empty<XElement>())
            {
                if (!string.Equals(cond.Attribute("scope")?.Value, "primary-catalogue", StringComparison.OrdinalIgnoreCase)) continue;
                var condType = cond.Attribute("type")?.Value;
                var childId = cond.Attribute("childId")?.Value;
                if (string.IsNullOrEmpty(condType) || string.IsNullOrEmpty(childId)) continue;
                yield return new BsDataDetachmentVisibility
                {
                    UnitId = unitId,
                    ConditionType = condType,
                    CatalogueId = childId,
                };
            }
        }
    }

    /// <summary>
    /// Detects the "hide unless detachment selected" pattern on an <c>&lt;entryLink&gt;</c> element
    /// and returns the inverted unlock <c>(modifiers, conditions)</c> JSON strings to store on the
    /// <see cref="BsDataEntryLink"/>.  Returns <c>(null, null)</c> if the pattern is absent.
    /// <para>
    /// Pattern detected:
    /// <code>
    /// &lt;modifier type="set" value="true" field="hidden"&gt;
    ///   &lt;conditionGroups&gt;&lt;conditionGroup type="and"&gt;&lt;conditions&gt;
    ///     &lt;condition scope="roster" field="selections" type="lessThan" value="1" childId="&lt;DETACHMENT-ID&gt;"/&gt;
    ///   &lt;/conditions&gt;&lt;/conditionGroup&gt;&lt;/conditionGroups&gt;
    /// &lt;/modifier&gt;
    /// </code>
    /// Conditions with <c>field="forces"</c> (Crusade mode marker) are ignored because they
    /// never apply in standard matched-play and do not represent detachment restrictions.
    /// </para>
    /// </summary>
    private static (string? Modifiers, string? Conditions) ParseEntryLinkDetachmentDependency(XElement entryLink)
    {
        foreach (var mod in entryLink.Element(Ns + "modifiers")?.Elements(Ns + "modifier") ?? Enumerable.Empty<XElement>())
        {
            if (!string.Equals(mod.Attribute("type")?.Value, "set", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(mod.Attribute("field")?.Value, "hidden", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(mod.Attribute("value")?.Value, "true", StringComparison.OrdinalIgnoreCase)) continue;

            // Look inside conditionGroups/conditionGroup[@type='and']/conditions
            var conditionGroups = mod.Element(Ns + "conditionGroups");
            if (conditionGroups == null) continue;

            foreach (var group in conditionGroups.Elements(Ns + "conditionGroup"))
            {
                if (!string.Equals(group.Attribute("type")?.Value, "and", StringComparison.OrdinalIgnoreCase)) continue;

                // Collect only roster+selections conditions (detachment references).
                // Ignore field="forces" conditions (Crusade mode — never active in matched play).
                var rosterConditions = group
                    .Element(Ns + "conditions")
                    ?.Elements(Ns + "condition")
                    .Where(c =>
                        string.Equals(c.Attribute("scope")?.Value, "roster", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Attribute("field")?.Value, "selections", StringComparison.OrdinalIgnoreCase))
                    .Select(c => new
                    {
                        field = c.Attribute("field")?.Value,
                        scope = c.Attribute("scope")?.Value,
                        // Invert: lessThan 1 (not selected) → atLeast 1 (selected)
                        type = "atLeast",
                        value = "1",
                        childId = c.Attribute("childId")?.Value,
                    })
                    .Where(c => !string.IsNullOrEmpty(c.childId))
                    .ToList();

                if (rosterConditions is { Count: > 0 })
                {
                    var unlockModifiers = JsonSerializer.Serialize(new[]
                    {
                        new { field = "hidden", type = "set", value = "false" },
                    });
                    var unlockConditions = JsonSerializer.Serialize(rosterConditions);
                    return (unlockModifiers, unlockConditions);
                }
            }
        }

        return (null, null);
    }
}
