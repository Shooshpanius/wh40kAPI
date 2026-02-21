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
                // Keep track of which profile tables we've initialized in this run
                var initializedProfileTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in profiles)
                {
                    if (string.IsNullOrEmpty(p.Id)) continue;
                    // Ensure TypeName is non-null
                    p.TypeName ??= string.Empty;

                    var compositeKey = $"{p.UnitId}|{p.Id}|{p.TypeName}";
                    if (!seenProfileIds.Add(compositeKey))
                    {
                        logger.LogDebug("Skipping duplicate profile {CompositeKey} from {File}", compositeKey, fileName);
                        continue;
                    }

                    // Determine target table name based on TypeName
                    var tableName = GetProfileTableName(p.TypeName);

                    // Ensure table exists and is truncated on first use
                    if (!initializedProfileTables.Contains(tableName))
                    {
                        await EnsureProfileTableExistsAsync(tableName);
                        // Clear previous data in that table for fresh import
                        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE `{tableName}`;");
                        initializedProfileTables.Add(tableName);
                    }

                    // Insert into specific table using parameterized SQL
                    await InsertProfileIntoTableAsync(tableName, p);
                }

                if (newUnits.Count > 0)
                {
                    db.Units.AddRange(newUnits);
                    totalUnits += newUnits.Count;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import {File}", fileName);
            }
        }

        await db.SaveChangesAsync();
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

        // Parse sharedSelectionEntries (top-level reusable units)
        foreach (var entry in root
            .Element(Ns + "sharedSelectionEntries")
            ?.Elements(Ns + "selectionEntry") ?? Enumerable.Empty<XElement>())
        {
            ExtractEntry(entry, id, units, profiles, seenUnitIds);
        }

        return (catalogue, units, profiles);
    }

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

        // Get points cost
        var points = entry
            .Element(Ns + "costs")
            ?.Elements(Ns + "cost")
            .Where(c => (c.Attribute("name")?.Value ?? "").Contains("pts", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Attribute("value")?.Value)
            .FirstOrDefault();

        units.Add(new KtBsDataUnit
        {
            Id = unitId,
            CatalogueId = catalogueId,
            Name = unitName,
            EntryType = entryType,
            Points = points,
        });

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

    private static string GetProfileTableName(string typeName)
    {
        // Sanitize type name to use in table name: allow letters, numbers and underscore
        if (string.IsNullOrWhiteSpace(typeName)) return "kt_profiles_default";
        var sanitized = System.Text.RegularExpressions.Regex.Replace(typeName.ToLowerInvariant(), "[^a-z0-9_]", "_");
        if (sanitized.Length == 0) return "kt_profiles_default";
        // Limit length to avoid overly long table names
        if (sanitized.Length > 50) sanitized = sanitized.Substring(0, 50);
        return $"kt_profiles_{sanitized}";
    }

    private async Task EnsureProfileTableExistsAsync(string tableName)
    {
        // Create table if not exists with columns matching KtBsDataProfile (TypeName stored as well)
        var createSql = $@"CREATE TABLE IF NOT EXISTS `{tableName}` (
            `UnitId` varchar(255) NOT NULL,
            `Id` varchar(255) NOT NULL,
            `Name` varchar(255) NULL,
            `TypeName` varchar(255) NULL,
            `Characteristics` longtext NULL,
            PRIMARY KEY (`UnitId`,`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        await db.Database.ExecuteSqlRawAsync(createSql);
    }

    private async Task InsertProfileIntoTableAsync(string tableName, KtBsDataProfile p)
    {
        // Use parameterized interpolated SQL for values; table name is sanitized by GetProfileTableName
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO `{tableName}` (UnitId, Id, Name, TypeName, Characteristics) VALUES ({p.UnitId}, {p.Id}, {p.Name}, {p.TypeName}, {p.Characteristics}) ON DUPLICATE KEY UPDATE Name = VALUES(Name), TypeName = VALUES(TypeName), Characteristics = VALUES(Characteristics);");
    }
}
