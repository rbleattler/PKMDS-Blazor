#!/usr/bin/env python3
"""
Generate tm-data.json from a saved Bulbapedia "List of TMs" HTML page.

The output maps each game version key to a dict of TM/TR-number -> move-name.
TM numbers preserve their zero-padded format from the page (e.g. "01", "001").
TR entries for Sword/Shield use "TR00"-"TR99" keys in the gen8swsh section.

Usage:
    python generate-tm-data.py --input "path/to/List of TMs - Bulbapedia*.html"
                               [--output /path/to/output]

Arguments:
    --input   Path to the saved Bulbapedia HTML file.
              (https://bulbapedia.bulbagarden.net/wiki/List_of_TMs)
    --output  Output directory for tm-data.json.
              Defaults to ../Pkmds.Rcl/wwwroot/data/ relative to this script.

Table order on the Bulbapedia page (12 tables in order):
    gen1, gen2, gen3, gen4, gen5, gen6,
    gen7sm, gen7lgpe, gen8swsh, gen8bdsp, gen9sv, gen9za

Sword/Shield TR data is sourced from PKHeX.Core PersonalInfo8SWSH.MachineMovesRecord
(the Bulbapedia equivalent is at:
 https://bulbapedia.bulbagarden.net/wiki/List_of_TMs_and_TRs_in_Pok%C3%A9mon_Sword_and_Shield)
and merged into gen8swsh under "TR00"-"TR99" keys.

Game-version-key mapping (used by DescriptionService.ToTmDataKey):
    gen1     -> Red/Green/Blue/Yellow
    gen2     -> Gold/Silver/Crystal
    gen3     -> Ruby/Sapphire/Emerald/FireRed/LeafGreen/Colosseum/XD
    gen4     -> Diamond/Pearl/Platinum/HeartGold/SoulSilver
    gen5     -> Black/White/Black2/White2
    gen6     -> X/Y/OmegaRuby/AlphaSapphire
    gen7sm   -> Sun/Moon/UltraSun/UltraMoon
    gen7lgpe -> Let's Go Pikachu/Eevee
    gen8swsh -> Sword/Shield (TMs as "00"-"99", TRs as "TR00"-"TR99")
    gen8bdsp -> BrilliantDiamond/ShiningPearl
    gen9sv   -> Scarlet/Violet
    gen9za   -> Legends Z-A
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# Sword/Shield TR move IDs (TR00-TR99)
# Source: PKHeX.Core PersonalInfo8SWSH.MachineMovesRecord
# Ref:    https://bulbapedia.bulbagarden.net/wiki/List_of_TMs_and_TRs_in_Pok%C3%A9mon_Sword_and_Shield
# ---------------------------------------------------------------------------
SWSH_TR_MOVE_IDS: list[int] = [
     14,  34,  53,  56,  57,  58,  59,  67,  85,  87,
     89,  94,  97, 116, 118, 126, 127, 133, 141, 161,
    164, 179, 188, 191, 200, 473, 203, 214, 224, 226,
    227, 231, 242, 247, 248, 253, 257, 269, 271, 276,
    285, 299, 304, 315, 322, 330, 334, 337, 339, 347,
    348, 349, 360, 370, 390, 394, 396, 398, 399, 402,
    404, 405, 406, 408, 411, 412, 413, 414, 417, 428,
    430, 437, 438, 441, 442, 444, 446, 447, 482, 484,
    486, 492, 500, 502, 503, 526, 528, 529, 535, 542,
    583, 599, 605, 663, 667, 675, 676, 706, 710, 776,
]


def build_swsh_tr_map(move_info: dict) -> dict[str, str]:
    """Build a TR00-TR99 -> move name mapping using PokeAPI move-info.json for names."""
    result: dict[str, str] = {}
    for i, move_id in enumerate(SWSH_TR_MOVE_IDS):
        entry = move_info.get(str(move_id))
        if entry:
            result[f"TR{i:02d}"] = entry["name"]
    return result


# Fixed order of tables as they appear on the Bulbapedia page
TABLE_KEYS = [
    "gen1",
    "gen2",
    "gen3",
    "gen4",
    "gen5",
    "gen6",
    "gen7sm",
    "gen7lgpe",
    "gen8swsh",
    "gen8bdsp",
    "gen9sv",
    "gen9za",
]


def strip_tags(html: str) -> str:
    return re.sub(r"<[^>]+>", "", html)


def clean(text: str) -> str:
    return re.sub(r"\s+", " ", strip_tags(text)).strip()


def parse_tables(html: str) -> list[dict[str, str]]:
    """
    Extract each roundtable from the page and return a list of
    {tm_number: move_name} dicts, one per table in document order.
    """
    results: list[dict[str, str]] = []

    table_pattern = re.compile(
        r"<table[^>]*roundtable[^>]*>(.*?)</table>", re.DOTALL
    )
    row_pattern = re.compile(r"<tr[^>]*>(.*?)</tr>", re.DOTALL)
    cell_pattern = re.compile(r"<t[dh][^>]*>(.*?)</t[dh]>", re.DOTALL)

    for table_match in table_pattern.finditer(html):
        table_body = table_match.group(1)
        tm_map: dict[str, str] = {}

        for row_match in row_pattern.finditer(table_body):
            cells = [clean(c.group(1)) for c in cell_pattern.finditer(row_match.group(1))]
            if len(cells) < 2:
                continue
            tm_num = cells[0]
            move_name = cells[1]
            # Skip header rows
            if tm_num in ("#", "TM", "HM") or move_name in ("Move", ""):
                continue
            # Validate: TM number should be digits only
            if not tm_num.isdigit():
                continue
            tm_map[tm_num] = move_name

        results.append(tm_map)

    return results


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Generate tm-data.json from a saved Bulbapedia List of TMs page.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument(
        "--input",
        required=True,
        metavar="PATH",
        help="Path to the saved Bulbapedia HTML file.",
    )
    parser.add_argument(
        "--output",
        metavar="PATH",
        default=None,
        help="Output directory (default: ../Pkmds.Rcl/wwwroot/data/ relative to this script).",
    )
    args = parser.parse_args()

    input_path = Path(args.input).resolve()
    if not input_path.exists():
        print(f"ERROR: Input file not found: {input_path}", file=sys.stderr)
        sys.exit(1)

    if args.output:
        output_dir = Path(args.output).resolve()
    else:
        output_dir = Path(__file__).parent.parent / "Pkmds.Rcl" / "wwwroot" / "data"
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f"Reading HTML from : {input_path}")
    print(f"Writing JSON to   : {output_dir}")
    print()

    html = input_path.read_text(encoding="utf-8")
    tables = parse_tables(html)

    if len(tables) != len(TABLE_KEYS):
        print(
            f"WARNING: Expected {len(TABLE_KEYS)} tables, found {len(tables)}. "
            "The page structure may have changed.",
            file=sys.stderr,
        )

    # Load move-info.json to resolve TR move IDs -> names
    move_info_path = output_dir / "move-info.json"
    if not move_info_path.exists():
        print(
            f"WARNING: {move_info_path} not found — SWSH TR names will be omitted. "
            "Run generate-descriptions.py first.",
            file=sys.stderr,
        )
        move_info: dict = {}
    else:
        with open(move_info_path, encoding="utf-8") as f:
            move_info = json.load(f)

    result: dict[str, dict[str, str]] = {}
    for key, tm_map in zip(TABLE_KEYS, tables):
        if key == "gen8swsh":
            tr_map = build_swsh_tr_map(move_info)
            tm_map = {**tm_map, **tr_map}
            print(f"  {key}: {len(tm_map) - len(tr_map)} TMs + {len(tr_map)} TRs")
        else:
            print(f"  {key}: {len(tm_map)} TMs")
        result[key] = tm_map

    out_path = output_dir / "tm-data.json"
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, ensure_ascii=False, separators=(",", ":"))

    size_kb = out_path.stat().st_size / 1024
    print()
    print(f"Done -> {out_path.name} ({size_kb:.0f} KB)")


if __name__ == "__main__":
    main()
