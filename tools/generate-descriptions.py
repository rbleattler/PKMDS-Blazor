#!/usr/bin/env python3
"""
Generate flat JSON description files from PokeAPI CSV data for PKMDS-Blazor.

These files are consumed by DescriptionService in Pkmds.Rcl to power info
tooltips for moves, abilities, and items in the Pokémon editor and bag.

Usage:
    python generate-descriptions.py --pokeapi /path/to/pokeapi [--output /path/to/output]

Arguments:
    --pokeapi   Path to the PokeAPI repo root, or directly to its data/v2/csv directory.
    --output    Output directory for the generated JSON files.
                Defaults to ../Pkmds.Rcl/wwwroot/data/ relative to this script.

Output files:
    ability-info.json  — abilities indexed by PokeAPI numeric ID
    move-info.json     — moves indexed by PokeAPI numeric ID, with per-version-group stats
    item-info.json     — items indexed by lowercase English name (for cross-referencing with PKHeX)

Version-group changelog interpretation
---------------------------------------
move_changelog stores the OLD value that was in effect BEFORE the named version group.
Reading entries for a given field sorted ascending by VG gives a chain:
    [(VG=3, V3), (VG=11, V11)]  with current value Vc
means:
    VG 1–2   → V3
    VG 3–10  → V11
    VG 11+   → Vc
This is implemented in field_epochs() and used to produce a compact epoch list in each
move's "stats" array.  The service picks the entry with the largest fromVersionGroup ≤ target.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

# PokeAPI internal language ID for English
ENGLISH = '9'

# Fields tracked in move_changelog (damage_class_id is not tracked there)
MOVE_STAT_FIELDS = ['type_id', 'power', 'pp', 'accuracy', 'effect_id', 'effect_chance']


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def strip_markup(text: str) -> str:
    """Remove [display]{mechanic:x} PokeAPI markup, keeping the display text."""
    return re.sub(r'\[([^\]]+)\]\{[^}]+\}', r'\1', text)


def clean_text(text: str) -> str:
    """Strip PokeAPI markup, remove soft hyphens, and collapse whitespace."""
    text = strip_markup(text)
    # Soft hyphen before a newline is a word-wrap artifact: "fore­\nlegs" -> "forelegs"
    text = re.sub(r'\u00ad[\r\n]+\s*', '', text)
    text = text.replace('\u00ad', '')          # any remaining bare soft hyphens
    text = re.sub(r'[\r\n]+', ' ', text)       # remaining newlines -> space
    text = re.sub(r'[ \t]{2,}', ' ', text)     # collapse runs of spaces
    return text.strip()


def read_csv(path: Path) -> list[dict]:
    with open(path, newline='', encoding='utf-8') as f:
        return list(csv.DictReader(f))


def en_flavor(rows: list[dict], id_field: str) -> dict[str, dict[str, str]]:
    """
    Build a {item_id: {version_group_id: flavor_text}} mapping from a flavor-text CSV.
    Keeps only English rows.  Collapses soft hyphens and newlines in the text.
    """
    result: dict[str, dict[str, str]] = defaultdict(dict)
    for row in rows:
        if row.get('language_id') == ENGLISH:
            item_id = row[id_field]
            vg = row['version_group_id']
            result[item_id][vg] = clean_text(row['flavor_text'])
    return dict(result)


# ---------------------------------------------------------------------------
# Per-version-group stat epochs for moves
# ---------------------------------------------------------------------------

def field_epochs(field_changes: list[tuple[int, str]], current_val: str) -> list[tuple[int, str]]:
    """
    Given a list of (version_group_id, old_value) pairs sorted ascending, and the
    current value from moves.csv, return a list of (from_vg, value) epochs.

    See module docstring for the changelog interpretation.
    """
    if not field_changes:
        return [(1, current_val)]

    epochs: list[tuple[int, str]] = []
    for i, (vg, val) in enumerate(field_changes):
        from_vg = field_changes[i - 1][0] if i > 0 else 1
        epochs.append((from_vg, val))
    # Final epoch: from the last changelog VG onward, use the current value
    epochs.append((field_changes[-1][0], current_val))
    return epochs


def compute_stat_epochs(move: dict, changes: list[dict]) -> list[dict]:
    """
    Build a compact list of stat snapshots for a move, each tagged with the first
    version group where those stats apply.  Consecutive identical snapshots are merged.
    """
    # Per-field timelines: field -> [(from_vg, value), ...]
    timelines: dict[str, list[tuple[int, str]]] = {}
    for field in MOVE_STAT_FIELDS:
        field_changes = sorted(
            [(int(c['changed_in_version_group_id']), c[field])
             for c in changes if c.get(field)],
            key=lambda x: x[0],
        )
        timelines[field] = field_epochs(field_changes, move.get(field, ''))

    # Collect every unique from_vg across all fields
    all_from_vgs = sorted({vg for tl in timelines.values() for vg, _ in tl})

    def value_at(field: str, target_vg: int) -> str:
        """Return the field value for the given target version group."""
        val = timelines[field][0][1]
        for from_vg, v in timelines[field]:
            if from_vg <= target_vg:
                val = v
        return val

    epochs: list[dict] = []
    for from_vg in all_from_vgs:
        snapshot = {field: value_at(field, from_vg) for field in MOVE_STAT_FIELDS}
        if not epochs or epochs[-1] != {**snapshot, 'fromVersionGroup': epochs[-1]['fromVersionGroup']}:
            # Only append if stats differ from the previous epoch
            prev = {k: v for k, v in epochs[-1].items() if k != 'fromVersionGroup'} if epochs else None
            if snapshot != prev:
                epochs.append({'fromVersionGroup': from_vg, **snapshot})

    return epochs


# ---------------------------------------------------------------------------
# Generators
# ---------------------------------------------------------------------------

def generate_ability_info(csv_dir: Path) -> dict:
    abilities = {r['id']: r for r in read_csv(csv_dir / 'abilities.csv')}

    names: dict[str, str] = {}
    for r in read_csv(csv_dir / 'ability_names.csv'):
        if r['local_language_id'] == ENGLISH:
            names[r['ability_id']] = r['name']

    descriptions: dict[str, str] = {}
    for r in read_csv(csv_dir / 'ability_prose.csv'):
        if r['local_language_id'] == ENGLISH:
            descriptions[r['ability_id']] = clean_text(r['short_effect'])

    flavor = en_flavor(read_csv(csv_dir / 'ability_flavor_text.csv'), 'ability_id')

    result: dict[str, dict] = {}
    for ability_id, ability in abilities.items():
        if ability.get('is_main_series') == '0':
            continue
        name = names.get(ability_id)
        if not name:
            continue
        entry: dict = {
            'name': name,
            'description': descriptions.get(ability_id, ''),
        }
        if ability_id in flavor:
            entry['flavor'] = flavor[ability_id]
        result[ability_id] = entry

    return result


def generate_move_info(csv_dir: Path) -> dict:
    damage_classes = {'1': 'Status', '2': 'Physical', '3': 'Special'}

    type_names: dict[str, str] = {}
    for r in read_csv(csv_dir / 'type_names.csv'):
        if r['local_language_id'] == ENGLISH:
            type_names[r['type_id']] = r['name']

    effect_prose: dict[str, str] = {}
    for r in read_csv(csv_dir / 'move_effect_prose.csv'):
        if r['local_language_id'] == ENGLISH:
            effect_prose[r['move_effect_id']] = clean_text(r['short_effect'])

    move_names: dict[str, str] = {}
    for r in read_csv(csv_dir / 'move_names.csv'):
        if r['local_language_id'] == ENGLISH:
            move_names[r['move_id']] = r['name']

    flavor = en_flavor(read_csv(csv_dir / 'move_flavor_text.csv'), 'move_id')

    changelog_by_move: dict[str, list[dict]] = defaultdict(list)
    for r in read_csv(csv_dir / 'move_changelog.csv'):
        changelog_by_move[r['move_id']].append(r)

    # Target names (English)
    target_names: dict[str, str] = {}
    for r in read_csv(csv_dir / 'move_target_prose.csv'):
        if r['local_language_id'] == ENGLISH:
            target_names[r['move_target_id']] = r['name']

    # Flag identifiers: id -> identifier
    flag_ids: dict[str, str] = {r['id']: r['identifier'] for r in read_csv(csv_dir / 'move_flags.csv')}

    # Flags per move: move_id -> [identifier, ...]
    flags_by_move: dict[str, list[str]] = defaultdict(list)
    for r in read_csv(csv_dir / 'move_flag_map.csv'):
        identifier = flag_ids.get(r['move_flag_id'])
        if identifier:
            flags_by_move[r['move_id']].append(identifier)

    result: dict[str, dict] = {}
    for move in read_csv(csv_dir / 'moves.csv'):
        move_id = move['id']
        name = move_names.get(move_id)
        if not name:
            continue

        # Base description from move_effect_prose, substituting $effect_chance%
        raw_desc = effect_prose.get(move['effect_id'], '')
        effect_chance = move.get('effect_chance', '')
        if effect_chance and '$effect_chance%' in raw_desc:
            raw_desc = raw_desc.replace('$effect_chance%', f'{effect_chance}%')
        description = raw_desc

        # Version-group-aware stats
        epochs = compute_stat_epochs(move, changelog_by_move.get(move_id, []))

        # Resolve type name and category for each epoch
        resolved_stats = []
        for epoch in epochs:
            resolved_stats.append({
                'fromVersionGroup': epoch['fromVersionGroup'],
                'type': type_names.get(epoch['type_id'], ''),
                'category': damage_classes.get(move['damage_class_id'], ''),
                'power': int(epoch['power']) if epoch.get('power') else None,
                'pp': int(epoch['pp']) if epoch.get('pp') else None,
                'accuracy': int(epoch['accuracy']) if epoch.get('accuracy') else None,
            })

        entry: dict = {
            'name': name,
            'description': description,
            'target': target_names.get(move['target_id'], ''),
            'flags': flags_by_move.get(move_id, []),
            'stats': resolved_stats,
        }
        if move_id in flavor:
            entry['flavor'] = flavor[move_id]

        result[move_id] = entry

    return result


def generate_item_info(csv_dir: Path) -> dict:
    item_names: dict[str, str] = {}
    for r in read_csv(csv_dir / 'item_names.csv'):
        if r['local_language_id'] == ENGLISH:
            item_names[r['item_id']] = r['name']

    descriptions: dict[str, str] = {}
    for r in read_csv(csv_dir / 'item_prose.csv'):
        if r['local_language_id'] == ENGLISH:
            descriptions[r['item_id']] = clean_text(r['short_effect'])

    flavor = en_flavor(read_csv(csv_dir / 'item_flavor_text.csv'), 'item_id')

    result: dict[str, dict] = {}
    for item in read_csv(csv_dir / 'items.csv'):
        item_id = item['id']
        name = item_names.get(item_id)
        if not name:
            continue
        # Key by normalized lowercase name so PKHeX names can cross-reference without ID mapping
        key = name.lower().strip()
        entry: dict = {
            'name': name,
            'description': descriptions.get(item_id, ''),
        }
        if item_id in flavor:
            entry['flavor'] = flavor[item_id]
        result[key] = entry

    return result


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(
        description='Generate description JSON files from PokeAPI CSV data.',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument(
        '--pokeapi',
        required=True,
        metavar='PATH',
        help='Path to PokeAPI repo root or its data/v2/csv directory.',
    )
    parser.add_argument(
        '--output',
        metavar='PATH',
        default=None,
        help='Output directory (default: ../Pkmds.Rcl/wwwroot/data/ relative to this script).',
    )
    args = parser.parse_args()

    pokeapi_path = Path(args.pokeapi).resolve()
    csv_dir = pokeapi_path / 'data' / 'v2' / 'csv' if (pokeapi_path / 'data').exists() else pokeapi_path
    if not csv_dir.is_dir():
        print(f'ERROR: CSV directory not found: {csv_dir}', file=sys.stderr)
        sys.exit(1)

    if args.output:
        output_dir = Path(args.output).resolve()
    else:
        output_dir = Path(__file__).parent.parent / 'Pkmds.Rcl' / 'wwwroot' / 'data'
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f'Reading CSV from : {csv_dir}')
    print(f'Writing JSON to  : {output_dir}')
    print()

    tasks = [
        ('ability-info.json', generate_ability_info, 'abilities'),
        ('move-info.json',    generate_move_info,    'moves'),
        ('item-info.json',    generate_item_info,    'items'),
    ]
    for filename, generator, label in tasks:
        print(f'Generating {filename}...')
        data = generator(csv_dir)
        out_path = output_dir / filename
        with open(out_path, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(data, f, ensure_ascii=False, separators=(',', ':'))
        size_kb = out_path.stat().st_size / 1024
        print(f'  {len(data):,} {label} -> {out_path.name} ({size_kb:.0f} KB)')

    print()
    print('Done.')


if __name__ == '__main__':
    main()
