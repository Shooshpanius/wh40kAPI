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

        // Format A: units defined directly in root selectionEntries with embedded profiles
        foreach (var entry in root
            .Element(Ns + "selectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(entry, id, units, profiles, seenUnitIds);
        }

        // Format B: units listed as root entryLinks, profiles stored in sharedSelectionEntries.
        // Build a lookup of sharedSelectionEntries by id so we can resolve targetId references.
        var sharedEntriesById = (root
            .Element(Ns + "sharedSelectionEntries")
            ?.Elements(Ns + "selectionEntry")
            ?? Enumerable.Empty<XElement>())
            .Select(e => (Id: e.Attribute("id")?.Value, Entry: e))
            .Where(x => !string.IsNullOrEmpty(x.Id))
            .ToDictionary(x => x.Id!, x => x.Entry);

        foreach (var link in root
            .Element(Ns + "entryLinks")
            ?.Elements(Ns + "entryLink") ?? Enumerable.Empty<XElement>())
        {
            var linkId = link.Attribute("id")?.Value;
            var targetId = link.Attribute("targetId")?.Value;
            if (string.IsNullOrEmpty(linkId) || string.IsNullOrEmpty(targetId)) continue;
            if (!sharedEntriesById.TryGetValue(targetId, out var sharedEntry)) continue;

            // Only process model-type entries (operatives), not weapons/upgrades
            var entryType = sharedEntry.Attribute("type")?.Value;
            if (entryType != "model") continue;

            if (seenUnitIds.Contains(linkId)) continue;
            seenUnitIds.Add(linkId);

            var linkName = link.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(linkName))
                linkName = sharedEntry.Attribute("name")?.Value ?? "";

            // Points: check link first, fall back to shared entry
            var points = GetPoints(link) ?? GetPoints(sharedEntry);

            units.Add(new KtBsDataUnit
            {
                Id = linkId,
                CatalogueId = id,
                Name = linkName,
                EntryType = entryType,
                Points = points,
            });

            // Profiles come from the shared entry; UnitId is the link's id
            ExtractProfiles(sharedEntry, linkId, profiles);
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

    private static void ExtractEntry(
        XElement entry,
        string catalogueId,
        List<KtBsDataUnit> units,
        List<KtBsDataProfile> profiles,
        HashSet<string> seenUnitIds)
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
        });

        ExtractProfiles(entry, unitId, profiles);
    }

    private static void ExtractProfiles(XElement entry, string unitId, List<KtBsDataProfile> profiles)
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
    }

}
