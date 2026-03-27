#!/usr/bin/env dotnet-run
#:property PublishAot=false
/*
 * Generate flat JSON description files from PokeAPI CSV data for PKMDS-Blazor.
 *
 * These files are consumed by DescriptionService in Pkmds.Rcl to power info
 * tooltips for moves, abilities, and items in the Pokémon editor and bag.
 *
 * Usage:
 *   dotnet run generate-descriptions.cs -- --pokeapi /path/to/pokeapi [--output /path/to/output]
 *
 * Arguments:
 *   --pokeapi   Path to the PokeAPI repo root, or directly to its data/v2/csv directory.
 *   --output    Output directory for the generated JSON files.
 *               Defaults to ../Pkmds.Rcl/wwwroot/data/ relative to this script.
 *
 * Output files:
 *   ability-info.json  — abilities indexed by PokeAPI numeric ID
 *   move-info.json     — moves indexed by PokeAPI numeric ID, with per-version-group stats
 *   item-info.json     — items indexed by lowercase English name (for cross-referencing with PKHeX)
 *
 * Version-group changelog interpretation
 * ---------------------------------------
 * move_changelog stores the OLD value that was in effect BEFORE the named version group.
 * Reading entries for a given field sorted ascending by VG gives a chain:
 *     [(VG=3, V3), (VG=11, V11)]  with current value Vc
 * means:
 *     VG 1–2   → V3
 *     VG 3–10  → V11
 *     VG 11+   → Vc
 * This is implemented in FieldEpochs() and used to produce a compact epoch list in each
 * move's "stats" array. The service picks the entry with the largest fromVersionGroup ≤ target.
 */

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// ---------------------------------------------------------------------------
// Args
// ---------------------------------------------------------------------------

string? pokeapiArg = null;
string? outputArg = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--pokeapi" && i + 1 < args.Length) pokeapiArg = args[++i];
    else if (args[i] == "--output" && i + 1 < args.Length) outputArg = args[++i];
}

if (pokeapiArg is null)
{
    Console.Error.WriteLine("Usage: dotnet run generate-descriptions.cs -- --pokeapi /path/to/pokeapi [--output /path/to/output]");
    return 1;
}

var pokeapiRoot = Path.GetFullPath(pokeapiArg);
var csvDir = Directory.Exists(Path.Combine(pokeapiRoot, "data"))
    ? Path.Combine(pokeapiRoot, "data", "v2", "csv")
    : pokeapiRoot;

if (!Directory.Exists(csvDir))
{
    Console.Error.WriteLine($"ERROR: CSV directory not found: {csvDir}");
    return 1;
}

var outputDir = outputArg is not null
    ? Path.GetFullPath(outputArg)
    : FindDefaultOutputDir();

Directory.CreateDirectory(outputDir);

Console.WriteLine($"Reading CSV from : {csvDir}");
Console.WriteLine($"Writing JSON to  : {outputDir}");
Console.WriteLine();

// Walk up from the working directory to find the repo root (contains Pkmds.Rcl/).
string FindDefaultOutputDir()
{
    var dir = Environment.CurrentDirectory;
    while (dir is not null)
    {
        var candidate = Path.Combine(dir, "Pkmds.Rcl", "wwwroot", "data");
        if (Directory.Exists(Path.Combine(dir, "Pkmds.Rcl")))
            return candidate;
        dir = Path.GetDirectoryName(dir);
    }
    throw new DirectoryNotFoundException("Could not find Pkmds.Rcl/ in any parent directory. Use --output to specify the output path.");
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const string English = "9";
string[] MoveStatFields = ["type_id", "power", "pp", "accuracy", "effect_id", "effect_chance"];

string StripMarkup(string text) =>
    Regex.Replace(text, @"\[([^\]]+)\]\{[^}]+\}", "$1");

string CleanText(string text)
{
    text = StripMarkup(text);
    text = Regex.Replace(text, "\u00ad[\r\n]+\\s*", "");   // soft hyphen before newline = word-wrap artifact
    text = text.Replace("\u00ad", "");                      // remaining bare soft hyphens
    text = Regex.Replace(text, @"[\r\n]+", " ");            // newlines → space
    text = Regex.Replace(text, @"[ \t]{2,}", " ");          // collapse spaces
    return text.Trim();
}

// Full CSV parser: handles quoted fields with embedded commas, newlines, and "" escapes.
List<List<string>> ParseCsv(string text)
{
    var records = new List<List<string>>();
    var field = new StringBuilder();
    var current = new List<string>();
    var inQuotes = false;
    var i = 0;
    while (i < text.Length)
    {
        var c = text[i];
        if (inQuotes)
        {
            if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i += 2; }
            else if (c == '"') { inQuotes = false; i++; }
            else { field.Append(c); i++; }
        }
        else
        {
            if (c == '"') { inQuotes = true; i++; }
            else if (c == ',') { current.Add(field.ToString()); field.Clear(); i++; }
            else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                current.Add(field.ToString()); field.Clear();
                records.Add(current); current = []; i += 2;
            }
            else if (c == '\n')
            {
                current.Add(field.ToString()); field.Clear();
                records.Add(current); current = []; i++;
            }
            else { field.Append(c); i++; }
        }
    }
    if (field.Length > 0 || current.Count > 0)
    {
        current.Add(field.ToString());
        records.Add(current);
    }
    // Drop trailing empty record from trailing newline
    if (records.Count > 0 && records[^1] is [""]) records.RemoveAt(records.Count - 1);
    return records;
}

List<Dictionary<string, string>> ReadCsv(string path)
{
    var records = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
    if (records.Count == 0) return [];
    var headers = records[0];
    var result = new List<Dictionary<string, string>>(records.Count - 1);
    for (var i = 1; i < records.Count; i++)
    {
        var values = records[i];
        var row = new Dictionary<string, string>(headers.Count);
        for (var j = 0; j < headers.Count && j < values.Count; j++)
            row[headers[j]] = values[j];
        result.Add(row);
    }
    return result;
}

// Build { itemId: { versionGroupId: flavorText } } for English rows.
Dictionary<string, Dictionary<string, string>> EnFlavor(List<Dictionary<string, string>> rows, string idField)
{
    var result = new Dictionary<string, Dictionary<string, string>>();
    foreach (var row in rows)
    {
        if (!row.TryGetValue("language_id", out var lang) || lang != English) continue;
        var itemId = row[idField];
        var vg = row["version_group_id"];
        if (!result.TryGetValue(itemId, out var vgMap)) result[itemId] = vgMap = [];
        vgMap[vg] = CleanText(row["flavor_text"]);
    }
    return result;
}

JsonObject ToJsonObject(Dictionary<string, string> dict)
{
    var obj = new JsonObject();
    foreach (var (k, v) in dict) obj[k] = v;
    return obj;
}

// ---------------------------------------------------------------------------
// Move stat epoch logic
// ---------------------------------------------------------------------------

// Given sorted (vg, oldValue) pairs and the current value, return (fromVg, value) epochs.
// See module docstring for the changelog interpretation.
List<(int fromVg, string value)> FieldEpochs(List<(int vg, string val)> changes, string currentVal)
{
    if (changes.Count == 0)
        return [(1, currentVal)];

    var epochs = new List<(int, string)>();
    for (var i = 0; i < changes.Count; i++)
    {
        var fromVg = i > 0 ? changes[i - 1].vg : 1;
        epochs.Add((fromVg, changes[i].val));
    }
    epochs.Add((changes[^1].vg, currentVal));
    return epochs;
}

List<Dictionary<string, string>> ComputeStatEpochs(
    Dictionary<string, string> move,
    List<Dictionary<string, string>> changes)
{
    var timelines = new Dictionary<string, List<(int fromVg, string value)>>();
    foreach (var field in MoveStatFields)
    {
        var fieldChanges = changes
            .Where(c => c.TryGetValue(field, out var v) && !string.IsNullOrEmpty(v))
            .Select(c => (vg: int.Parse(c["changed_in_version_group_id"]), val: c[field]))
            .OrderBy(x => x.vg)
            .ToList();
        move.TryGetValue(field, out var current);
        timelines[field] = FieldEpochs(fieldChanges, current ?? "");
    }

    var allFromVgs = timelines.Values
        .SelectMany(tl => tl.Select(x => x.fromVg))
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    string ValueAt(string field, int targetVg)
    {
        var val = timelines[field][0].value;
        foreach (var (fromVg, v) in timelines[field])
            if (fromVg <= targetVg) val = v;
        return val;
    }

    var epochs = new List<Dictionary<string, string>>();
    foreach (var fromVg in allFromVgs)
    {
        var snapshot = MoveStatFields.ToDictionary(f => f, f => ValueAt(f, fromVg));
        if (epochs.Count == 0 || !MoveStatFields.All(f => epochs[^1][f] == snapshot[f]))
        {
            var epoch = new Dictionary<string, string>(snapshot) { ["fromVersionGroup"] = fromVg.ToString() };
            epochs.Add(epoch);
        }
    }
    return epochs;
}

// ---------------------------------------------------------------------------
// Generators
// ---------------------------------------------------------------------------

JsonObject GenerateAbilityInfo(string csvDir)
{
    var abilities = ReadCsv(Path.Combine(csvDir, "abilities.csv"))
        .ToDictionary(r => r["id"]);

    var names = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "ability_names.csv")))
        if (r["local_language_id"] == English) names[r["ability_id"]] = r["name"];

    var descriptions = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "ability_prose.csv")))
        if (r["local_language_id"] == English) descriptions[r["ability_id"]] = CleanText(r["short_effect"]);

    var flavor = EnFlavor(ReadCsv(Path.Combine(csvDir, "ability_flavor_text.csv")), "ability_id");

    var result = new JsonObject();
    foreach (var (abilityId, ability) in abilities)
    {
        if (ability.TryGetValue("is_main_series", out var main) && main == "0") continue;
        if (!names.TryGetValue(abilityId, out var name)) continue;

        var entry = new JsonObject
        {
            ["name"] = name,
            ["description"] = descriptions.GetValueOrDefault(abilityId, ""),
        };
        if (flavor.TryGetValue(abilityId, out var flavorMap))
            entry["flavor"] = ToJsonObject(flavorMap);
        result[abilityId] = entry;
    }
    return result;
}

JsonObject GenerateMoveInfo(string csvDir)
{
    var damageClasses = new Dictionary<string, string> { ["1"] = "Status", ["2"] = "Physical", ["3"] = "Special" };

    var typeNames = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "type_names.csv")))
        if (r["local_language_id"] == English) typeNames[r["type_id"]] = r["name"];

    var effectProse = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_effect_prose.csv")))
        if (r["local_language_id"] == English) effectProse[r["move_effect_id"]] = CleanText(r["short_effect"]);

    var moveNames = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_names.csv")))
        if (r["local_language_id"] == English) moveNames[r["move_id"]] = r["name"];

    var flavor = EnFlavor(ReadCsv(Path.Combine(csvDir, "move_flavor_text.csv")), "move_id");

    var changelogByMove = new Dictionary<string, List<Dictionary<string, string>>>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_changelog.csv")))
    {
        var id = r["move_id"];
        if (!changelogByMove.TryGetValue(id, out var list)) changelogByMove[id] = list = [];
        list.Add(r);
    }

    var targetNames = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_target_prose.csv")))
        if (r["local_language_id"] == English) targetNames[r["move_target_id"]] = r["name"];

    var flagIds = ReadCsv(Path.Combine(csvDir, "move_flags.csv"))
        .ToDictionary(r => r["id"], r => r["identifier"]);

    var flagsByMove = new Dictionary<string, List<string>>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_flag_map.csv")))
    {
        if (!flagIds.TryGetValue(r["move_flag_id"], out var identifier)) continue;
        var id = r["move_id"];
        if (!flagsByMove.TryGetValue(id, out var list)) flagsByMove[id] = list = [];
        list.Add(identifier);
    }

    // PokeAPI does not track wind or slicing flags (Gen IX mechanics). Supplement from Showdown data.
    // Identifiers match moves.csv `identifier` column (hyphenated lowercase).
    HashSet<string> windMoveIdentifiers =
    [
        "aeroblast", "air-cutter", "bleakwind-storm", "blizzard", "fairy-wind",
        "gust", "heat-wave", "hurricane", "icy-wind", "petal-blizzard",
        "sandsear-storm", "sandstorm", "springtide-storm", "tailwind", "twister",
        "whirlwind", "wildbolt-storm",
    ];
    HashSet<string> slicingMoveIdentifiers =
    [
        "aerial-ace", "air-cutter", "air-slash", "aqua-cutter", "behemoth-blade",
        "bitter-blade", "ceaseless-edge", "cross-poison", "cut", "fury-cutter",
        "kowtow-cleave", "leaf-blade", "mighty-cleave", "night-slash", "population-bomb",
        "psyblade", "psycho-cut", "razor-leaf", "razor-shell", "sacred-sword",
        "secret-sword", "slash", "solar-blade", "stone-axe", "tachyon-cutter", "x-scissor",
    ];

    // move_meta: secondary effects (ailment, flinch, drain, healing, multi-hit, crit rate)
    var moveMetaById = new Dictionary<string, Dictionary<string, string>>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_meta.csv")))
        moveMetaById[r["move_id"]] = r;

    // ailment names (English only)
    var ailmentNames = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_meta_ailment_names.csv")))
        if (r["local_language_id"] == English) ailmentNames[r["move_meta_ailment_id"]] = r["name"];

    // stat changes per move
    var statChangesByMove = new Dictionary<string, List<(string stat, int change)>>();
    var statNameById = new Dictionary<string, string>
    {
        ["1"] = "HP", ["2"] = "Attack", ["3"] = "Defense",
        ["4"] = "Sp. Atk", ["5"] = "Sp. Def", ["6"] = "Speed",
        ["7"] = "Accuracy", ["8"] = "Evasion",
    };
    foreach (var r in ReadCsv(Path.Combine(csvDir, "move_meta_stat_changes.csv")))
    {
        var id = r["move_id"];
        if (!statChangesByMove.TryGetValue(id, out var list)) statChangesByMove[id] = list = [];
        var statName = statNameById.GetValueOrDefault(r["stat_id"], r["stat_id"]);
        list.Add((statName, int.Parse(r["change"])));
    }

    var result = new JsonObject();
    foreach (var move in ReadCsv(Path.Combine(csvDir, "moves.csv")))
    {
        var moveId = move["id"];
        if (!moveNames.TryGetValue(moveId, out var name)) continue;

        move.TryGetValue("effect_id", out var effectId);
        var rawDesc = effectId is not null ? effectProse.GetValueOrDefault(effectId, "") : "";
        move.TryGetValue("effect_chance", out var effectChance);
        if (!string.IsNullOrEmpty(effectChance) && rawDesc.Contains("$effect_chance%"))
            rawDesc = rawDesc.Replace("$effect_chance%", $"{effectChance}%");

        var epochs = ComputeStatEpochs(move, changelogByMove.GetValueOrDefault(moveId, []));

        var resolvedStats = new JsonArray();
        foreach (var epoch in epochs)
        {
            move.TryGetValue("damage_class_id", out var dcId);
            var stat = new JsonObject
            {
                ["fromVersionGroup"] = int.Parse(epoch["fromVersionGroup"]),
                ["type"] = typeNames.GetValueOrDefault(epoch.GetValueOrDefault("type_id", ""), ""),
                ["category"] = dcId is not null ? damageClasses.GetValueOrDefault(dcId, "") : "",
                ["power"] = !string.IsNullOrEmpty(epoch.GetValueOrDefault("power")) ? int.Parse(epoch["power"]) : JsonValue.Create<int?>(null),
                ["pp"] = !string.IsNullOrEmpty(epoch.GetValueOrDefault("pp")) ? int.Parse(epoch["pp"]) : JsonValue.Create<int?>(null),
                ["accuracy"] = !string.IsNullOrEmpty(epoch.GetValueOrDefault("accuracy")) ? int.Parse(epoch["accuracy"]) : JsonValue.Create<int?>(null),
            };
            resolvedStats.Add(stat);
        }

        move.TryGetValue("priority", out var priorityStr);
        var priority = int.TryParse(priorityStr, out var p) ? p : 0;

        move.TryGetValue("target_id", out var targetId);
        move.TryGetValue("identifier", out var moveIdentifier);
        var moveFlags = flagsByMove.TryGetValue(moveId, out var flags) ? [..flags] : new List<string>();
        if (moveIdentifier is not null && windMoveIdentifiers.Contains(moveIdentifier))
            moveFlags.Add("wind");
        if (moveIdentifier is not null && slicingMoveIdentifiers.Contains(moveIdentifier))
            moveFlags.Add("slicing");
        var entry = new JsonObject
        {
            ["name"] = name,
            ["description"] = rawDesc,
            ["target"] = targetId is not null ? targetNames.GetValueOrDefault(targetId, "") : "",
            ["flags"] = new JsonArray(moveFlags.Select(f => JsonValue.Create(f)).ToArray<JsonNode?>()),
            ["stats"] = resolvedStats,
        };
        if (priority != 0)
            entry["priority"] = priority;
        if (flavor.TryGetValue(moveId, out var flavorMap))
            entry["flavor"] = ToJsonObject(flavorMap);

        // Secondary effects from move_meta
        if (moveMetaById.TryGetValue(moveId, out var meta))
        {
            var metaObj = new JsonObject();

            var ailmentIdStr = meta.GetValueOrDefault("meta_ailment_id", "0");
            if (int.TryParse(ailmentIdStr, out var ailmentId) && ailmentId != 0)
            {
                metaObj["ailmentId"] = ailmentId;
                if (ailmentNames.TryGetValue(ailmentIdStr, out var ailmentName))
                    metaObj["ailmentName"] = ailmentName;
            }
            if (int.TryParse(meta.GetValueOrDefault("ailment_chance", "0"), out var ailmentChance) && ailmentChance > 0)
                metaObj["ailmentChance"] = ailmentChance;
            if (int.TryParse(meta.GetValueOrDefault("flinch_chance", "0"), out var flinchChance) && flinchChance > 0)
                metaObj["flinchChance"] = flinchChance;
            if (int.TryParse(meta.GetValueOrDefault("drain", "0"), out var drain) && drain != 0)
                metaObj["drain"] = drain;
            if (int.TryParse(meta.GetValueOrDefault("healing", "0"), out var healing) && healing != 0)
                metaObj["healing"] = healing;
            var minHitsStr = meta.GetValueOrDefault("min_hits", "");
            var maxHitsStr = meta.GetValueOrDefault("max_hits", "");
            if (!string.IsNullOrEmpty(minHitsStr)) metaObj["minHits"] = int.Parse(minHitsStr);
            if (!string.IsNullOrEmpty(maxHitsStr)) metaObj["maxHits"] = int.Parse(maxHitsStr);
            if (int.TryParse(meta.GetValueOrDefault("crit_rate", "0"), out var critRate) && critRate > 0)
                metaObj["critRate"] = critRate;
            if (int.TryParse(meta.GetValueOrDefault("stat_chance", "0"), out var statChance) && statChance > 0)
                metaObj["statChance"] = statChance;

            if (statChangesByMove.TryGetValue(moveId, out var statChanges) && statChanges.Count > 0)
            {
                var changesArr = new JsonArray();
                foreach (var (stat, change) in statChanges)
                    changesArr.Add(new JsonObject { ["stat"] = stat, ["change"] = change });
                metaObj["statChanges"] = changesArr;
            }

            if (metaObj.Count > 0)
                entry["meta"] = metaObj;
        }

        result[moveId] = entry;
    }
    return result;
}

JsonObject GenerateItemInfo(string csvDir)
{
    var itemNames = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "item_names.csv")))
        if (r["local_language_id"] == English) itemNames[r["item_id"]] = r["name"];

    var descriptions = new Dictionary<string, string>();
    foreach (var r in ReadCsv(Path.Combine(csvDir, "item_prose.csv")))
        if (r["local_language_id"] == English) descriptions[r["item_id"]] = CleanText(r["short_effect"]);

    var flavor = EnFlavor(ReadCsv(Path.Combine(csvDir, "item_flavor_text.csv")), "item_id");

    var result = new JsonObject();
    foreach (var item in ReadCsv(Path.Combine(csvDir, "items.csv")))
    {
        var itemId = item["id"];
        if (!itemNames.TryGetValue(itemId, out var name)) continue;
        var key = name.ToLowerInvariant().Trim();
        var entry = new JsonObject
        {
            ["name"] = name,
            ["description"] = descriptions.GetValueOrDefault(itemId, ""),
        };
        if (flavor.TryGetValue(itemId, out var flavorMap))
            entry["flavor"] = ToJsonObject(flavorMap);
        result[key] = entry;
    }
    return result;
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

var serializerOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = false,
};

var tasks = new (string file, Func<string, JsonObject> generator, string label)[]
{
    ("ability-info.json", GenerateAbilityInfo, "abilities"),
    ("move-info.json",    GenerateMoveInfo,    "moves"),
    ("item-info.json",    GenerateItemInfo,    "items"),
};

foreach (var (file, generator, label) in tasks)
{
    Console.Write($"Generating {file}...");
    var data = generator(csvDir);
    var outPath = Path.Combine(outputDir, file);
    File.WriteAllText(outPath, data.ToJsonString(serializerOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    var sizeKb = new FileInfo(outPath).Length / 1024;
    Console.WriteLine($"  {data.Count:N0} {label} -> {file} ({sizeKb} KB)");
}

Console.WriteLine();
Console.WriteLine("Done.");
return 0;
