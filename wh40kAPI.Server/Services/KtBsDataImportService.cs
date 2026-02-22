using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Xml.Linq;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models.KtBsData;

namespace wh40kAPI.Server.Services;

public class KtBsDataImportService(KtBsDataDbContext db, IHttpClientFactory httpClientFactory, ILogger<KtBsDataImportService> logger)
{
    private const string GithubApiBase = "https://api.github.com/repos/BSData/wh40k-killteam/contents";
    private const string GithubRawBase = "https://raw.githubusercontent.com/BSData/wh40k-killteam/main/";
    private static readonly XNamespace Ns = "http://www.battlescribe.net/schema/catalogueSchema";

    public async Task<int> ImportAsync()
    {
        var client = httpClientFactory.CreateClient("github");

        // 1. Fetch list of .cat files from the repository
        var catFiles = await FetchCatFileListAsync(client);
        logger.LogInformation("Found {Count} .cat files to import", catFiles.Count);

        // 2. Clear existing KT BSData
        await db.Profiles.ExecuteDeleteAsync();
        await db.Units.ExecuteDeleteAsync();
        await db.Catalogues.ExecuteDeleteAsync();

        int totalUnits = 0;
        int totalProfiles = 0;

        // Keep global seen sets to avoid adding entities with duplicate primary keys
        var seenCatalogueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 3. Process each .cat file
        foreach (var (fileName, downloadUrl) in catFiles)
        {
            try
            {
                logger.LogInformation("Importing {File}", fileName);
                var xml = await client.GetStringAsync(downloadUrl);
                var (catalogue, units, profiles) = ParseCatalogueXml(xml);

                if (catalogue == null)
                    continue;

                // Skip catalogue if its id was already imported
                if (!seenCatalogueIds.Add(catalogue.Id))
                {
                    logger.LogWarning("Skipping catalogue {Id} from {File} — duplicate id", catalogue.Id, fileName);
                }
                else
                {
                    db.Catalogues.Add(catalogue);
                }

                // Filter units and profiles by global seen ids to prevent duplicate primary key inserts
                var newUnits = new List<KtBsDataUnit>();
                foreach (var u in units)
                {
                    if (string.IsNullOrEmpty(u.Id)) continue;
                    // Use composite key CatalogueId|UnitId so same unit id can exist in different catalogues
                    var unitKey = $"{u.CatalogueId}|{u.Id}";
                    if (!seenUnitIds.Add(unitKey))
                    {
                        logger.LogDebug("Skipping duplicate unit {UnitKey} from {File}", unitKey, fileName);
                        continue;
                    }
                    newUnits.Add(u);
                }

                var newProfiles = new List<KtBsDataProfile>();
                foreach (var p in profiles)
                {
                    if (string.IsNullOrEmpty(p.Id)) continue;
                    p.TypeName ??= string.Empty;

                    var compositeKey = $"{p.UnitId}|{p.Id}|{p.TypeName}";
                    if (!seenProfileIds.Add(compositeKey))
                    {
                        logger.LogDebug("Skipping duplicate profile {CompositeKey} from {File}", compositeKey, fileName);
                        continue;
                    }

                    newProfiles.Add(p);
                }

                if (newUnits.Count > 0)
                    db.Units.AddRange(newUnits);

                if (newProfiles.Count > 0)
                    db.Profiles.AddRange(newProfiles);

                await db.SaveChangesAsync();
                totalUnits += newUnits.Count;
                totalProfiles += newProfiles.Count;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import {File}", fileName);
                db.ChangeTracker.Clear();
            }
        }

        logger.LogInformation("Import complete. Units: {TotalUnits}, Profiles: {TotalProfiles}", totalUnits, totalProfiles);
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
            if (!name.StartsWith("2024", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.EndsWith(".cat", StringComparison.OrdinalIgnoreCase))
                continue;

            var downloadUrl = item.TryGetProperty("download_url", out var dlProp)
                ? dlProp.GetString() ?? (GithubRawBase + Uri.EscapeDataString(name))
                : GithubRawBase + Uri.EscapeDataString(name);

            files.Add((name, downloadUrl));
        }

        return files;
    }

    private static (KtBsDataCatalogue? Catalogue, List<KtBsDataUnit> Units, List<KtBsDataProfile> Profiles)
        ParseCatalogueXml(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return (null, [], []); }

        var root = doc.Root;
        if (root == null) return (null, [], []);

        var id = root.Attribute("id")?.Value ?? "";
        var name = root.Attribute("name")?.Value ?? "";
        if (!int.TryParse(root.Attribute("revision")?.Value, out var revision))
            revision = 0;

        var catalogue = new KtBsDataCatalogue
        {
            Id = id,
            Name = name,
            Revision = revision,
            FetchedAt = DateTime.UtcNow,
        };

        var units = new List<KtBsDataUnit>();
        var profiles = new List<KtBsDataProfile>();
        var seenUnitIds = new HashSet<string>();

        // Build a combined lookup of all selection entries so entryLinks within model
        // entries (standard weapons) can be resolved.
        var entriesById = (root
                .Element(Ns + "selectionEntries")
                ?.Elements(Ns + "selectionEntry")
                ?? Enumerable.Empty<XElement>())
            .Concat(root
                .Element(Ns + "sharedSelectionEntries")
                ?.Elements(Ns + "selectionEntry")
                ?? Enumerable.Empty<XElement>())
            .Select(e => (Id: e.Attribute("id")?.Value, Entry: e))
            .Where(x => !string.IsNullOrEmpty(x.Id))
            .GroupBy(x => x.Id!)
            .ToDictionary(g => g.Key, g => g.First().Entry);

        // Format A: units defined directly in root selectionEntries with embedded profiles
        foreach (var entry in root
            .Element(Ns + "selectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(entry, id, units, profiles, seenUnitIds, entriesById);
        }

        // Format B: units listed as root entryLinks; the actual unit definition (with profiles)
        // is the selectionEntry whose id equals the entryLink's targetId.
        foreach (var link in root
            .Element(Ns + "entryLinks")
            ?.Elements(Ns + "entryLink") ?? Enumerable.Empty<XElement>())
        {
            var targetId = link.Attribute("targetId")?.Value;
            if (string.IsNullOrEmpty(targetId)) continue;
            if (!entriesById.TryGetValue(targetId, out var linkedEntry)) continue;

            // Only process model-type entries (operatives), not weapons/upgrades
            var entryType = linkedEntry.Attribute("type")?.Value;
            if (entryType != "model") continue;

            // The unit's canonical id is the targetId (= linkedEntry.id)
            if (seenUnitIds.Contains(targetId)) continue;
            seenUnitIds.Add(targetId);

            var linkName = link.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(linkName))
                linkName = linkedEntry.Attribute("name")?.Value ?? "";

            // Points: check link first, fall back to linked entry
            var points = GetPoints(link) ?? GetPoints(linkedEntry);

            units.Add(new KtBsDataUnit
            {
                Id = targetId,
                CatalogueId = id,
                Name = linkName,
                EntryType = entryType,
                Points = points,
                MinCount = GetMinCount(linkedEntry),
                MaxCount = GetMaxCount(linkedEntry),
                Categories = GetCategories(linkedEntry),
            });

            // Profiles come from the linked entry; UnitId is the targetId
            ExtractProfiles(linkedEntry, targetId, profiles, entriesById);
        }

        return (catalogue, units, profiles);
    }

    private static string? GetPoints(XElement entry) =>
        entry.Element(Ns + "costs")
            ?.Elements(Ns + "cost")
            .Where(c =>
            {
                var costName = c.Attribute("name")?.Value ?? "";
                return costName.Contains("pts", StringComparison.OrdinalIgnoreCase)
                    || costName.Contains("ep", StringComparison.OrdinalIgnoreCase);
            })
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();

    /// <summary>
    /// Returns the minimum selection count from constraints with field="selections".
    /// </summary>
    private static int? GetMinCount(XElement entry)
    {
        var value = entry.Element(Ns + "constraints")
            ?.Elements(Ns + "constraint")
            .Where(c => c.Attribute("field")?.Value == "selections"
                     && c.Attribute("type")?.Value == "min")
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();
        return int.TryParse(value, out var minCount) ? minCount : null;
    }

    /// <summary>
    /// Returns the maximum selection count from constraints with field="selections".
    /// Also falls back to conditions with type="instanceOf" and field="selections".
    /// </summary>
    private static int? GetMaxCount(XElement entry)
    {
        var value = entry.Element(Ns + "constraints")
            ?.Elements(Ns + "constraint")
            .Where(c => c.Attribute("field")?.Value == "selections"
                     && c.Attribute("type")?.Value == "max")
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();
        if (int.TryParse(value, out var maxCount)) return maxCount;

        // Fallback: condition type="instanceOf" field="selections"
        value = entry.Element(Ns + "conditions")
            ?.Elements(Ns + "condition")
            .Where(c => c.Attribute("type")?.Value == "instanceOf"
                     && c.Attribute("field")?.Value == "selections")
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();
        return int.TryParse(value, out maxCount) ? maxCount : null;
    }

    /// <summary>
    /// Returns a JSON array of category targetIds from categoryLinks.
    /// </summary>
    private static string? GetCategories(XElement entry)
    {
        var categoryIds = entry
            .Element(Ns + "categoryLinks")
            ?.Elements(Ns + "categoryLink")
            .Select(c => c.Attribute("targetId")?.Value)
            .Where(tid => !string.IsNullOrEmpty(tid))
            .ToList();

        return categoryIds is { Count: > 0 }
            ? JsonSerializer.Serialize(categoryIds)
            : null;
    }

    private static void ExtractEntry(
        XElement entry,
        string catalogueId,
        List<KtBsDataUnit> units,
        List<KtBsDataProfile> profiles,
        HashSet<string> seenUnitIds,
        IReadOnlyDictionary<string, XElement> entriesById)
    {
        var unitId = entry.Attribute("id")?.Value ?? "";
        if (string.IsNullOrEmpty(unitId) || seenUnitIds.Contains(unitId)) return;
        seenUnitIds.Add(unitId);

        var unitName = entry.Attribute("name")?.Value ?? "";
        var entryType = entry.Attribute("type")?.Value;

        units.Add(new KtBsDataUnit
        {
            Id = unitId,
            CatalogueId = catalogueId,
            Name = unitName,
            EntryType = entryType,
            Points = GetPoints(entry),
            MinCount = GetMinCount(entry),
            MaxCount = GetMaxCount(entry),
            Categories = GetCategories(entry),
        });

        ExtractProfiles(entry, unitId, profiles, entriesById);
    }

    private static void ExtractProfiles(
        XElement entry,
        string unitId,
        List<KtBsDataProfile> profiles,
        IReadOnlyDictionary<string, XElement> entriesById)
    {
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

            profiles.Add(new KtBsDataProfile
            {
                Id = profileId,
                UnitId = unitId,
                Name = profileName,
                TypeName = typeName ?? string.Empty,
                Characteristics = characteristics != null
                    ? JsonSerializer.Serialize(characteristics)
                    : null,
            });
        }

        // Follow entryLinks to extract standard weapon profiles from shared entries.
        foreach (var link in entry
            .Element(Ns + "entryLinks")
            ?.Elements(Ns + "entryLink") ?? Enumerable.Empty<XElement>())
        {
            var targetId = link.Attribute("targetId")?.Value;
            if (string.IsNullOrEmpty(targetId)) continue;
            if (!entriesById.TryGetValue(targetId, out var linkedEntry)) continue;
            ExtractProfiles(linkedEntry, unitId, profiles, entriesById);
        }

        // Recurse into nested selectionEntries (e.g., special upgrade weapons).
        foreach (var nested in entry
            .Element(Ns + "selectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractProfiles(nested, unitId, profiles, entriesById);
        }

        // Recurse into selectionEntryGroups (e.g., grouped weapon options).
        foreach (var group in entry
            .Element(Ns + "selectionEntryGroups")
            ?.Elements(Ns + "selectionEntryGroup") ?? Enumerable.Empty<XElement>())
        {
            ExtractProfiles(group, unitId, profiles, entriesById);
        }
    }

}
