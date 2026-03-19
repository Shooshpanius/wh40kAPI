using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Services;

public class DataImportService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<DataImportService> logger)
{
    private const string ExcelSpecUrl = "https://wahapedia.ru/wh40k10ed/Export%20Data%20Specs.xlsx";

    private static readonly string[] KnownCsvFiles =
    [
        "Factions.csv", "Abilities.csv", "Source.csv", "Datasheets.csv",
        "Datasheets_abilities.csv", "Datasheets_detachment_abilities.csv",
        "Datasheets_enhancements.csv", "Datasheets_keywords.csv",
        "Datasheets_leader.csv", "Datasheets_models.csv", "Datasheets_models_cost.csv",
        "Datasheets_options.csv", "Datasheets_stratagems.csv", "Datasheets_unit_composition.csv",
        "Datasheets_wargear.csv", "Detachment_abilities.csv", "Detachments.csv",
        "Enhancements.csv", "Stratagems.csv", "Last_update.csv",
    ];

    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        Delimiter = "|",
        HasHeaderRecord = true,
        BadDataFound = null,
        MissingFieldFound = null,
        TrimOptions = TrimOptions.Trim,
        PrepareHeaderForMatch = args => args.Header.ToLowerInvariant().TrimStart('\uFEFF'),
    };

    /// <summary>
    /// Downloads the wahapedia Export Data Specs Excel file, extracts the CSV download URLs
    /// from its hyperlinks, downloads each CSV file, and imports the data into the database.
    /// </summary>
    public async Task ImportFromWahapediaAsync()
    {
        var client = httpClientFactory.CreateClient("wahapedia");

        // Step 1: Download the Excel spec file
        logger.LogInformation("Downloading Export Data Specs from {Url}", ExcelSpecUrl);
        HttpResponseMessage excelResponse;
        try
        {
            excelResponse = await client.GetAsync(ExcelSpecUrl);
            excelResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to download Export Data Specs from {ExcelSpecUrl}: {ex.Message}", ex);
        }

        // Step 2: Parse the Excel file for CSV download URLs
        using var excelStream = await excelResponse.Content.ReadAsStreamAsync();
        excelResponse.Dispose();
        var csvUrls = ParseExcelForCsvUrls(excelStream);
        logger.LogInformation("Found {Count} CSV URLs in the Excel spec", csvUrls.Count);

        // Merge parsed URLs with the known CSV file list, falling back to a constructed URL
        // for any file not explicitly linked in the Excel.
        const string wahapediaBase = "https://wahapedia.ru/wh40k10ed/";
        var fileUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileName in KnownCsvFiles)
        {
            fileUrls[fileName] = csvUrls.TryGetValue(fileName, out var url)
                ? url
                : wahapediaBase + fileName;
        }

        // Step 3: Download each CSV and import
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            foreach (var (fileName, url) in fileUrls)
            {
                logger.LogInformation("Downloading {File} from {Url}", fileName, url);
                try
                {
                    var csvResponse = await client.GetAsync(url);
                    csvResponse.EnsureSuccessStatusCode();
                    var csvBytes = await csvResponse.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(Path.Combine(tempDir, fileName), csvBytes);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to download CSV file '{fileName}' from {url}: {ex.Message}", ex);
                }
            }

            await ImportAllCsvFiles(tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Parses an XLSX stream (which is a ZIP archive) and extracts all external hyperlinks
    /// that point to CSV files.  Returns a dictionary of filename → URL.
    /// </summary>
    private static Dictionary<string, string> ParseExcelForCsvUrls(Stream excelStream)
    {
        var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XNamespace pkgRels = "http://schemas.openxmlformats.org/package/2006/relationships";

        using var zip = new ZipArchive(excelStream, ZipArchiveMode.Read, leaveOpen: true);

        // Build a map of relationship ID → worksheet target path from the workbook rels file.
        var sheetTargets = new List<string>();
        var wbRelsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (wbRelsEntry != null)
        {
            using var relsStream = wbRelsEntry.Open();
            var relsDoc = XDocument.Load(relsStream);
            foreach (var rel in relsDoc.Descendants(pkgRels + "Relationship"))
            {
                var type = rel.Attribute("Type")?.Value ?? "";
                var target = rel.Attribute("Target")?.Value ?? "";
                if (type.EndsWith("/worksheet") && !string.IsNullOrEmpty(target))
                    sheetTargets.Add(target); // e.g., "worksheets/sheet1.xml"
            }
        }

        // For each worksheet, read its relationships file and collect CSV hyperlinks.
        foreach (var sheetTarget in sheetTargets)
        {
            var sheetFileName = Path.GetFileName(sheetTarget); // "sheet1.xml"
            var sheetRelsPath = $"xl/worksheets/_rels/{sheetFileName}.rels";
            var sheetRelsEntry = zip.GetEntry(sheetRelsPath);
            if (sheetRelsEntry == null) continue;

            using var sheetRelsStream = sheetRelsEntry.Open();
            var sheetRelsDoc = XDocument.Load(sheetRelsStream);

            foreach (var rel in sheetRelsDoc.Descendants(pkgRels + "Relationship"))
            {
                var type = rel.Attribute("Type")?.Value ?? "";
                var targetMode = rel.Attribute("TargetMode")?.Value ?? "";
                var target = rel.Attribute("Target")?.Value ?? "";

                if (type.EndsWith("/hyperlink")
                    && targetMode == "External"
                    && Uri.TryCreate(target, UriKind.Absolute, out var uri)
                    && uri.AbsolutePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(uri.AbsolutePath);
                    urls[fileName] = target;
                }
            }
        }

        return urls;
    }

    private async Task ImportAllCsvFiles(string dir)
    {
        await ImportCsv<FactionRow>(dir, "Factions.csv", records =>
        {
            db.Factions.RemoveRange(db.Factions);
            db.Factions.AddRange(records.Select(r => new Models.Faction
            {
                Id = r.id ?? "",
                Name = r.name ?? "",
                Link = r.link,
            }));
        });

        await ImportCsv<AbilityRow>(dir, "Abilities.csv", records =>
        {
            db.Abilities.RemoveRange(db.Abilities);
            db.Abilities.AddRange(records.Select(r => new Ability
            {
                Id = r.id ?? "",
                Name = r.name ?? "",
                Legend = r.legend,
                FactionId = r.faction_id,
                Description = r.description,
            }));
        });

        await ImportCsv<SourceRow>(dir, "Source.csv", records =>
        {
            db.Sources.RemoveRange(db.Sources);
            db.Sources.AddRange(records.Select(r => new Source
            {
                Id = r.id ?? "",
                Name = r.name ?? "",
                Type = r.type,
                Edition = r.edition,
                Version = r.version,
                ErrataDate = r.errata_date,
                ErrataLink = r.errata_link,
            }));
        });

        await ImportCsv<DatasheetRow>(dir, "Datasheets.csv", records =>
        {
            db.Datasheets.RemoveRange(db.Datasheets);
            db.Datasheets.AddRange(records.Select(r => new Datasheet
            {
                Id = r.id ?? "",
                Name = r.name ?? "",
                FactionId = r.faction_id,
                SourceId = r.source_id,
                Legend = r.legend,
                Role = r.role,
                Loadout = r.loadout,
                Transport = r.transport,
                IsVirtual = r.virtual_col,
                LeaderHead = r.leader_head,
                LeaderFooter = r.leader_footer,
                DamagedW = r.damaged_w,
                DamagedDescription = r.damaged_description,
                Link = r.link,
            }));
        });

        await ImportCsv<DatasheetAbilityRow>(dir, "Datasheets_abilities.csv", records =>
        {
            db.DatasheetAbilities.RemoveRange(db.DatasheetAbilities);
            db.DatasheetAbilities.AddRange(records.Select(r => new DatasheetAbility
            {
                DatasheetId = r.datasheet_id ?? "",
                Line = ParseIntSafe(r.line),
                AbilityId = r.ability_id,
                Model = r.model,
                Name = r.name,
                Description = r.description,
                Type = r.type,
                Parameter = r.parameter,
            }));
        });

        await ImportCsv<DatasheetDetachmentAbilityRow>(dir, "Datasheets_detachment_abilities.csv", records =>
        {
            db.DatasheetDetachmentAbilities.RemoveRange(db.DatasheetDetachmentAbilities);
            db.DatasheetDetachmentAbilities.AddRange(records.Select(r => new DatasheetDetachmentAbility
            {
                DatasheetId = r.datasheet_id ?? "",
                DetachmentAbilityId = r.detachment_ability_id ?? "",
            }));
        });

        await ImportCsv<DatasheetEnhancementRow>(dir, "Datasheets_enhancements.csv", records =>
        {
            db.DatasheetEnhancements.RemoveRange(db.DatasheetEnhancements);
            db.DatasheetEnhancements.AddRange(records.Select(r => new DatasheetEnhancement
            {
                DatasheetId = r.datasheet_id ?? "",
                EnhancementId = r.enhancement_id ?? "",
            }));
        });

        await ImportCsv<DatasheetKeywordRow>(dir, "Datasheets_keywords.csv", records =>
        {
            db.DatasheetKeywords.RemoveRange(db.DatasheetKeywords);
            db.DatasheetKeywords.AddRange(records
                .DistinctBy(r => (r.datasheet_id, r.keyword, r.model))
                .Select(r => new DatasheetKeyword
                {
                    DatasheetId = r.datasheet_id ?? "",
                    Keyword = r.keyword ?? "",
                    Model = r.model ?? "",
                    IsFactionKeyword = r.is_faction_keyword,
                }));
        });

        await ImportCsv<DatasheetLeaderRow>(dir, "Datasheets_leader.csv", records =>
        {
            db.DatasheetLeaders.RemoveRange(db.DatasheetLeaders);
            db.DatasheetLeaders.AddRange(records
                .DistinctBy(r => (r.leader_id, r.attached_id))
                .Select(r => new DatasheetLeader
                {
                    LeaderId = r.leader_id ?? "",
                    AttachedId = r.attached_id ?? "",
                }));
        });

        await ImportCsv<DatasheetModelRow>(dir, "Datasheets_models.csv", records =>
        {
            db.DatasheetModels.RemoveRange(db.DatasheetModels);
            db.DatasheetModels.AddRange(records.Select(r => new DatasheetModel
            {
                DatasheetId = r.datasheet_id ?? "",
                Line = ParseIntSafe(r.line),
                Name = r.name,
                M = r.m,
                T = r.t,
                Sv = r.sv,
                InvSv = r.inv_sv,
                InvSvDescr = r.inv_sv_descr,
                W = r.w,
                Ld = r.ld,
                OC = r.oc,
                BaseSize = r.base_size,
                BaseSizeDescr = r.base_size_descr,
            }));
        });

        await ImportCsv<DatasheetModelCostRow>(dir, "Datasheets_models_cost.csv", records =>
        {
            db.DatasheetModelCosts.RemoveRange(db.DatasheetModelCosts);
            db.DatasheetModelCosts.AddRange(records.Select(r => new DatasheetModelCost
            {
                DatasheetId = r.datasheet_id ?? "",
                Line = ParseIntSafe(r.line),
                Description = r.description,
                Cost = r.cost,
            }));
        });

        await ImportCsv<DatasheetOptionRow>(dir, "Datasheets_options.csv", records =>
        {
            db.DatasheetOptions.RemoveRange(db.DatasheetOptions);
            db.DatasheetOptions.AddRange(records.Select(r => new DatasheetOption
            {
                DatasheetId = r.datasheet_id ?? "",
                Line = ParseIntSafe(r.line),
                Button = r.button,
                Description = r.description,
            }));
        });

        await ImportCsv<DatasheetStratagemRow>(dir, "Datasheets_stratagems.csv", records =>
        {
            db.DatasheetStratagems.RemoveRange(db.DatasheetStratagems);
            db.DatasheetStratagems.AddRange(records.Select(r => new DatasheetStratagem
            {
                DatasheetId = r.datasheet_id ?? "",
                StratagemId = r.stratagem_id ?? "",
            }));
        });

        await ImportCsv<DatasheetUnitCompositionRow>(dir, "Datasheets_unit_composition.csv", records =>
        {
            db.DatasheetUnitCompositions.RemoveRange(db.DatasheetUnitCompositions);
            db.DatasheetUnitCompositions.AddRange(records.Select(r => new DatasheetUnitComposition
            {
                DatasheetId = r.datasheet_id ?? "",
                Line = ParseIntSafe(r.line),
                Description = r.description,
            }));
        });

        await ImportCsv<DatasheetWargearRow>(dir, "Datasheets_wargear.csv", records =>
        {
            db.DatasheetWargears.RemoveRange(db.DatasheetWargears);
            db.DatasheetWargears.AddRange(records
                .DistinctBy(r => (r.datasheet_id, r.line, r.line_in_wargear))
                .Select(r => new DatasheetWargear
                {
                    DatasheetId = r.datasheet_id ?? "",
                    Line = ParseIntSafe(r.line),
                    LineInWargear = ParseIntSafe(r.line_in_wargear),
                    Dice = r.dice,
                    Name = r.name,
                    Description = r.description,
                    Range = r.range,
                    Type = r.type,
                    A = r.a,
                    BsWs = r.bs_ws,
                    S = r.s,
                    AP = r.ap,
                    D = r.d,
                }));
        });

        await ImportCsv<DetachmentAbilityRow>(dir, "Detachment_abilities.csv", records =>
        {
            db.DetachmentAbilities.RemoveRange(db.DetachmentAbilities);
            db.DetachmentAbilities.AddRange(records.Select(r => new DetachmentAbility
            {
                Id = r.id ?? "",
                FactionId = r.faction_id ?? "",
                Name = r.name ?? "",
                Legend = r.legend,
                Description = r.description,
                Detachment = r.detachment,
                DetachmentId = r.detachment_id,
            }));
        });

        await ImportCsv<DetachmentRow>(dir, "Detachments.csv", records =>
        {
            db.Detachments.RemoveRange(db.Detachments);
            db.Detachments.AddRange(records.Select(r => new Detachment
            {
                Id = r.id ?? "",
                FactionId = r.faction_id,
                Name = r.name ?? "",
                Legend = r.legend,
                Type = r.type,
            }));
        });

        await ImportCsv<EnhancementRow>(dir, "Enhancements.csv", records =>
        {
            db.Enhancements.RemoveRange(db.Enhancements);
            db.Enhancements.AddRange(records.Select(r => new Enhancement
            {
                Id = r.id ?? "",
                FactionId = r.faction_id,
                Name = r.name ?? "",
                Cost = r.cost,
                Detachment = r.detachment,
                DetachmentId = r.detachment_id,
                Legend = r.legend,
                Description = r.description,
            }));
        });

        await ImportCsv<StratagemRow>(dir, "Stratagems.csv", records =>
        {
            db.Stratagems.RemoveRange(db.Stratagems);
            db.Stratagems.AddRange(records.Select(r => new Stratagem
            {
                Id = r.id ?? "",
                FactionId = r.faction_id,
                Name = r.name ?? "",
                Type = r.type,
                CpCost = r.cp_cost,
                Legend = r.legend,
                Turn = r.turn,
                Phase = r.phase,
                Detachment = r.detachment,
                DetachmentId = r.detachment_id,
                Description = r.description,
            }));
        });

        await ImportCsv<LastUpdateRow>(dir, "Last_update.csv", records =>
        {
            db.LastUpdates.RemoveRange(db.LastUpdates);
            var row = records.FirstOrDefault();
            if (row != null)
                db.LastUpdates.Add(new LastUpdate { Id = 1, UpdatedAt = row.last_update });
        });

        await db.SaveChangesAsync();
    }

    private async Task ImportCsv<T>(string dir, string fileName, Action<List<T>> importer)
    {
        var filePath = Path.Combine(dir, fileName);
        if (!File.Exists(filePath)) return;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CsvConfig);
        var records = csv.GetRecords<T>().ToList();
        importer(records);
        await Task.CompletedTask;
    }

    private static int ParseIntSafe(string? value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    // CSV row types for CsvHelper (property names must match CSV column headers)
    private class FactionRow
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? link { get; set; }
    }

    private class AbilityRow
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? legend { get; set; }
        public string? faction_id { get; set; }
        public string? description { get; set; }
    }

    private class SourceRow
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public string? edition { get; set; }
        public string? version { get; set; }
        public string? errata_date { get; set; }
        public string? errata_link { get; set; }
    }

    private class DatasheetRow
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? faction_id { get; set; }
        public string? source_id { get; set; }
        public string? legend { get; set; }
        public string? role { get; set; }
        public string? loadout { get; set; }
        public string? transport { get; set; }
        [CsvHelper.Configuration.Attributes.Name("virtual")]
        public string? virtual_col { get; set; }
        public string? leader_head { get; set; }
        public string? leader_footer { get; set; }
        public string? damaged_w { get; set; }
        public string? damaged_description { get; set; }
        public string? link { get; set; }
    }

    private class DatasheetAbilityRow
    {
        public string? datasheet_id { get; set; }
        public string? line { get; set; }
        public string? ability_id { get; set; }
        public string? model { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? type { get; set; }
        public string? parameter { get; set; }
    }

    private class DatasheetDetachmentAbilityRow
    {
        public string? datasheet_id { get; set; }
        public string? detachment_ability_id { get; set; }
    }

    private class DatasheetEnhancementRow
    {
        public string? datasheet_id { get; set; }
        public string? enhancement_id { get; set; }
    }

    private class DatasheetKeywordRow
    {
        public string? datasheet_id { get; set; }
        public string? keyword { get; set; }
        public string? model { get; set; }
        public string? is_faction_keyword { get; set; }
    }

    private class DatasheetLeaderRow
    {
        public string? leader_id { get; set; }
        public string? attached_id { get; set; }
    }

    private class DatasheetModelRow
    {
        public string? datasheet_id { get; set; }
        public string? line { get; set; }
        public string? name { get; set; }
        public string? m { get; set; }
        public string? t { get; set; }
        public string? sv { get; set; }
        public string? inv_sv { get; set; }
        public string? inv_sv_descr { get; set; }
        public string? w { get; set; }
        public string? ld { get; set; }
        public string? oc { get; set; }
        public string? base_size { get; set; }
        public string? base_size_descr { get; set; }
    }

    private class DatasheetModelCostRow
    {
        public string? datasheet_id { get; set; }
        public string? line { get; set; }
        public string? description { get; set; }
        public string? cost { get; set; }
    }

    private class DatasheetOptionRow
    {
        public string? datasheet_id { get; set; }
        public string? line { get; set; }
        public string? button { get; set; }
        public string? description { get; set; }
    }

    private class DatasheetStratagemRow
    {
        public string? datasheet_id { get; set; }
        public string? stratagem_id { get; set; }
    }

    private class DatasheetUnitCompositionRow
    {
        public string? datasheet_id { get; set; }
        public string? line { get; set; }
        public string? description { get; set; }
    }

    private class DatasheetWargearRow
    {
        public string? datasheet_id { get; set; }
        public string? line { get; set; }
        public string? line_in_wargear { get; set; }
        public string? dice { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? range { get; set; }
        public string? type { get; set; }
        public string? a { get; set; }
        public string? bs_ws { get; set; }
        public string? s { get; set; }
        public string? ap { get; set; }
        public string? d { get; set; }
    }

    private class DetachmentAbilityRow
    {
        public string? id { get; set; }
        public string? faction_id { get; set; }
        public string? name { get; set; }
        public string? legend { get; set; }
        public string? description { get; set; }
        public string? detachment { get; set; }
        public string? detachment_id { get; set; }
    }

    private class DetachmentRow
    {
        public string? id { get; set; }
        public string? faction_id { get; set; }
        public string? name { get; set; }
        public string? legend { get; set; }
        public string? type { get; set; }
    }

    private class EnhancementRow
    {
        public string? faction_id { get; set; }
        public string? id { get; set; }
        public string? name { get; set; }
        public string? cost { get; set; }
        public string? detachment { get; set; }
        public string? detachment_id { get; set; }
        public string? legend { get; set; }
        public string? description { get; set; }
    }

    private class StratagemRow
    {
        public string? faction_id { get; set; }
        public string? name { get; set; }
        public string? id { get; set; }
        public string? type { get; set; }
        public string? cp_cost { get; set; }
        public string? legend { get; set; }
        public string? turn { get; set; }
        public string? phase { get; set; }
        public string? detachment { get; set; }
        public string? detachment_id { get; set; }
        public string? description { get; set; }
    }

    private class LastUpdateRow
    {
        public string? last_update { get; set; }
    }
}
