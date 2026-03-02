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

        // Keep global seen sets to avoid adding entities with duplicate primary keys
        var seenCatalogueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenInfoLinkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEntryLinkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenConstraintIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 3. Process each .cat file
        foreach (var (fileName, downloadUrl) in catFiles)
        {
            try
            {
                logger.LogInformation("Importing {File}", fileName);
                var xml = await client.GetStringAsync(downloadUrl);
                var (catalogue, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups) = ParseCatalogueXml(xml);

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
                    if (!seenConstraintIds.Add(c.Id))
                    {
                        logger.LogDebug("Skipping duplicate constraint {ConstraintId} from {File}", c.Id, fileName);
                        continue;
                    }
                    newConstraints.Add(c);
                }

                // ModifierGroups use a DB-generated int PK so no seen-set needed;
                // filter only for accepted units.
                var newModifierGroups = modifierGroups.Where(g => acceptedUnitIds.Contains(g.UnitId)).ToList();

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

                await db.SaveChangesAsync();
                totalUnits += newUnits.Count;
                totalProfiles += newProfiles.Count;
                totalCategories += newCategories.Count;
                totalInfoLinks += newInfoLinks.Count;
                totalEntryLinks += newEntryLinks.Count;
                totalConstraints += newConstraints.Count;
                totalModifierGroups += newModifierGroups.Count;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import {File}", fileName);
                db.ChangeTracker.Clear();
            }
        }

        logger.LogInformation("Import complete. Units: {TotalUnits}, Profiles: {TotalProfiles}, Categories: {TotalCategories}, InfoLinks: {TotalInfoLinks}, EntryLinks: {TotalEntryLinks}, Constraints: {TotalConstraints}, ModifierGroups: {TotalModifierGroups}",
            totalUnits, totalProfiles, totalCategories, totalInfoLinks, totalEntryLinks, totalConstraints, totalModifierGroups);
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

    private static (BsDataCatalogue? Catalogue, List<BsDataUnit> Units, List<BsDataProfile> Profiles, List<BsDataUnitCategory> Categories, List<BsDataInfoLink> InfoLinks, List<BsDataEntryLink> EntryLinks, List<BsDataConstraint> Constraints, List<BsDataModifierGroup> ModifierGroups)
        ParseCatalogueXml(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return (null, [], [], [], [], [], [], []); }

        var root = doc.Root;
        if (root == null) return (null, [], [], [], [], [], [], []);

        var id = root.Attribute("id")?.Value ?? "";
        var name = root.Attribute("name")?.Value ?? "";
        if (!int.TryParse(root.Attribute("revision")?.Value, out var revision))
            revision = 0;
        var library = string.Equals(root.Attribute("library")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        var catalogue = new BsDataCatalogue
        {
            Id = id,
            Name = name,
            Revision = revision,
            Library = library,
            FetchedAt = DateTime.UtcNow,
        };

        var units = new List<BsDataUnit>();
        var profiles = new List<BsDataProfile>();
        var categories = new List<BsDataUnitCategory>();
        var infoLinks = new List<BsDataInfoLink>();
        var entryLinks = new List<BsDataEntryLink>();
        var constraints = new List<BsDataConstraint>();
        var modifierGroups = new List<BsDataModifierGroup>();
        var seenUnitIds = new HashSet<string>();

        // Parse sharedSelectionEntries (top-level reusable units)
        foreach (var entry in root
            .Element(Ns + "sharedSelectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(entry, id, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups, seenUnitIds);
        }

        // Also parse top-level selectionEntries within forces (if present)
        foreach (var force in root
            .Element(Ns + "entryLinks")
            ?.Elements(Ns + "entryLink") ?? Enumerable.Empty<XElement>())
        {
            // entry links are references; we skip them to avoid duplicates
        }

        return (catalogue, units, profiles, categories, infoLinks, entryLinks, constraints, modifierGroups);
    }

    private static void ExtractEntry(
        XElement entry,
        string catalogueId,
        List<BsDataUnit> units,
        List<BsDataProfile> profiles,
        List<BsDataUnitCategory> categories,
        List<BsDataInfoLink> infoLinks,
        List<BsDataEntryLink> entryLinks,
        List<BsDataConstraint> constraints,
        List<BsDataModifierGroup> modifierGroups,
        HashSet<string> seenUnitIds)
    {
        var unitId = entry.Attribute("id")?.Value ?? "";
        if (string.IsNullOrEmpty(unitId) || seenUnitIds.Contains(unitId)) return;
        seenUnitIds.Add(unitId);

        var unitName = entry.Attribute("name")?.Value ?? "";
        var entryType = entry.Attribute("type")?.Value;
        var hidden = string.Equals(entry.Attribute("hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        // Get points cost
        var points = entry
            .Element(Ns + "costs")
            ?.Elements(Ns + "cost")
            .Where(c => (c.Attribute("name")?.Value ?? "").Contains("pts", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();

        units.Add(new BsDataUnit
        {
            Id = unitId,
            CatalogueId = catalogueId,
            Name = unitName,
            EntryType = entryType,
            Points = points,
            Hidden = hidden,
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
            entryLinks.Add(new BsDataEntryLink
            {
                Id = linkId,
                UnitId = unitId,
                Name = el.Attribute("name")?.Value ?? "",
                Hidden = string.Equals(el.Attribute("hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                TargetId = targetId,
                Type = el.Attribute("type")?.Value,
            });
        }

        // Extract constraints from the entry itself and from all nested selectionEntryGroups
        var constraintSources =
            (entry.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>())
            .Concat(
                (entry.Element(Ns + "selectionEntryGroups")?.Elements(Ns + "selectionEntryGroup") ?? Enumerable.Empty<XElement>())
                .SelectMany(g => g.Element(Ns + "constraints")?.Elements(Ns + "constraint") ?? Enumerable.Empty<XElement>()));

        foreach (var c in constraintSources)
        {
            var constraintId = c.Attribute("id")?.Value ?? "";
            if (string.IsNullOrEmpty(constraintId)) continue;
            var field = c.Attribute("field")?.Value;
            var scope = c.Attribute("scope")?.Value;
            var value = c.Attribute("value")?.Value;
            var percentValue = string.Equals(c.Attribute("percentValue")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var shared = string.Equals(c.Attribute("shared")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var includeChildSelections = string.Equals(c.Attribute("includeChildSelections")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var includeChildForces = string.Equals(c.Attribute("includeChildForces")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var childId = c.Attribute("childId")?.Value;
            var type = c.Attribute("type")?.Value;

            constraints.Add(new BsDataConstraint
            {
                Id = constraintId,
                UnitId = unitId,
                Field = field,
                Scope = scope,
                Value = value,
                PercentValue = percentValue,
                Shared = shared,
                IncludeChildSelections = includeChildSelections,
                IncludeChildForces = includeChildForces,
                ChildId = childId,
                Type = type,
            });

            // For constraints with field="selections" scope="force", also write a copy
            // with scope="parent" to preserve the behavior where such constraints are
            // recorded as scope="parent" as well.
            if (string.Equals(field, "selections", StringComparison.Ordinal)
                && string.Equals(scope, "force", StringComparison.Ordinal))
            {
                constraints.Add(new BsDataConstraint
                {
                    Id = constraintId + "_parent",
                    UnitId = unitId,
                    Field = field,
                    Scope = "parent",
                    Value = value,
                    PercentValue = percentValue,
                    Shared = shared,
                    IncludeChildSelections = includeChildSelections,
                    IncludeChildForces = includeChildForces,
                    ChildId = childId,
                    Type = type,
                });
            }
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
    }
}
