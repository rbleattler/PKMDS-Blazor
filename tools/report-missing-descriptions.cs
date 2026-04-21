#!/usr/bin/env dotnet-run
#:property PublishAot=false
/*
 * Report entries in ability-info.json / move-info.json / item-info.json that are
 * missing a description, per-gen flavor, or both.
 *
 * This is a diagnostic tool — it reads the generated JSON files and produces a
 * plain-text report grouping missing entries by dataset and category. Use it to
 * track progress toward full coverage as PokeAPI / Showdown / other sources fill
 * in gaps upstream.
 *
 * Usage:
 *   dotnet run tools/report-missing-descriptions.cs [-- [--data <dir>] [--output <path>]]
 *
 * Arguments:
 *   --data    Directory containing ability-info.json, move-info.json, item-info.json.
 *             Defaults to Pkmds.Rcl/wwwroot/data/ under the repo root.
 *   --output  Output file path. Defaults to missing-flavor-report.txt at the repo root.
 *             Pass "-" to write to stdout.
 *
 * Categories:
 *   BOTH MISSING  — description empty AND no populated per-gen flavor entries
 *   DESCRIPTION   — description empty; flavor populated
 *   FLAVOR        — description populated; flavor map empty or absent
 */

using System.Text;
using System.Text.Json.Nodes;

string? dataArg = null;
string? outputArg = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--data" && i + 1 < args.Length) dataArg = args[++i];
    else if (args[i] == "--output" && i + 1 < args.Length) outputArg = args[++i];
}

var dataDir = dataArg is not null ? Path.GetFullPath(dataArg) : FindDefaultDataDir();
if (!Directory.Exists(dataDir))
{
    Console.Error.WriteLine($"ERROR: data directory not found: {dataDir}");
    return 1;
}

var outputPath = outputArg switch
{
    "-"  => null,
    null => Path.Combine(FindRepoRoot() ?? Environment.CurrentDirectory, "missing-flavor-report.txt"),
    _    => Path.GetFullPath(outputArg),
};

static string? FindRepoRoot()
{
    var dir = Environment.CurrentDirectory;
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir, "Pkmds.Rcl"))) return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

string FindDefaultDataDir()
{
    var root = FindRepoRoot()
        ?? throw new DirectoryNotFoundException("Could not find Pkmds.Rcl/ in any parent directory. Use --data to specify the data path.");
    return Path.Combine(root, "Pkmds.Rcl", "wwwroot", "data");
}

static bool IsEmpty(string? s) => string.IsNullOrWhiteSpace(s);

static bool AllFlavorsEmpty(JsonNode? flavor)
{
    if (flavor is not JsonObject obj || obj.Count == 0) return true;
    foreach (var (_, node) in obj)
        if (!IsEmpty(node?.GetValue<string>())) return false;
    return true;
}

static (List<string> Both, List<string> DescOnly, List<string> FlavorOnly) Classify(
    JsonObject data, bool prefixIdInLabel)
{
    var both = new List<string>();
    var descOnly = new List<string>();
    var flavorOnly = new List<string>();
    foreach (var (key, node) in data)
    {
        if (node is not JsonObject entry) continue;
        var name = (string?)entry["name"] ?? key;
        var label = prefixIdInLabel ? $"#{key} {name}" : name;
        var descEmpty = IsEmpty((string?)entry["description"]);
        var flavorEmpty = AllFlavorsEmpty(entry["flavor"]);
        if (descEmpty && flavorEmpty) both.Add(label);
        else if (descEmpty) descOnly.Add(label);
        else if (flavorEmpty) flavorOnly.Add(label);
    }
    both.Sort(StringComparer.OrdinalIgnoreCase);
    descOnly.Sort(StringComparer.OrdinalIgnoreCase);
    flavorOnly.Sort(StringComparer.OrdinalIgnoreCase);
    return (both, descOnly, flavorOnly);
}

static JsonObject LoadJson(string path) =>
    (JsonObject)JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!;

var abilitiesPath = Path.Combine(dataDir, "ability-info.json");
var movesPath = Path.Combine(dataDir, "move-info.json");
var itemsPath = Path.Combine(dataDir, "item-info.json");

foreach (var p in new[] { abilitiesPath, movesPath, itemsPath })
    if (!File.Exists(p))
    {
        Console.Error.WriteLine($"ERROR: expected JSON not found: {p}");
        return 1;
    }

var abilities = LoadJson(abilitiesPath);
var moves = LoadJson(movesPath);
var items = LoadJson(itemsPath);

var sb = new StringBuilder();
sb.AppendLine("=== Missing description/flavor report ===");
sb.AppendLine("Generated from Pkmds.Rcl/wwwroot/data/*.json after the latest generate-descriptions.cs run.");
sb.AppendLine("Rerun tools/report-missing-descriptions.cs to regenerate. Target: 0 entries everywhere.");
sb.AppendLine();
sb.AppendLine("Categories:");
sb.AppendLine("  BOTH MISSING  — empty description AND no populated per-gen flavor entries");
sb.AppendLine("  DESCRIPTION   — description empty; flavor populated");
sb.AppendLine("  FLAVOR        — description populated; flavor empty/absent");
sb.AppendLine();

void WriteSection(string label, JsonObject data, bool prefixIds)
{
    var (both, descOnly, flavorOnly) = Classify(data, prefixIds);
    sb.AppendLine($"== {label.ToUpperInvariant()} (total {data.Count}) ==");
    sb.AppendLine($"  both missing={both.Count}  description only={descOnly.Count}  flavor only={flavorOnly.Count}");
    sb.AppendLine();
    sb.AppendLine($"-- {label}: BOTH description and flavor missing ({both.Count}) --");
    foreach (var n in both) sb.AppendLine($"  {n}");
    sb.AppendLine();
    sb.AppendLine($"-- {label}: description only missing ({descOnly.Count}) --");
    foreach (var n in descOnly) sb.AppendLine($"  {n}");
    sb.AppendLine();
    sb.AppendLine($"-- {label}: flavor only missing ({flavorOnly.Count}) --");
    foreach (var n in flavorOnly) sb.AppendLine($"  {n}");
    sb.AppendLine();
}

WriteSection("Items", items, prefixIds: false);
WriteSection("Abilities", abilities, prefixIds: true);
WriteSection("Moves", moves, prefixIds: true);

if (outputPath is null)
{
    Console.Write(sb.ToString());
}
else
{
    File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Wrote {outputPath}");
}
return 0;
