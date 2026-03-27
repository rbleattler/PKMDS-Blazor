#!/usr/bin/env python3
"""
PostToolUse hook: regenerates data JSON files when generate-descriptions.cs
or generate-tm-data.cs is modified by Claude.
"""
import sys
import json
import re
import subprocess
import os

data = json.load(sys.stdin)
file_path = (
    (data.get("tool_input") or {}).get("file_path")
    or (data.get("tool_response") or {}).get("filePath")
    or ""
)
# Normalize separators
file_path = file_path.replace("\\", "/")

repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

if re.search(r"generate-descriptions[.]cs$", file_path):
    print("Regenerating ability-info.json, move-info.json, item-info.json...", flush=True)
    subprocess.run(
        [
            "dotnet", "run", "tools/generate-descriptions.cs", "--",
            "--pokeapi", "C:/Code/pokeapi",
            "--showdown", "C:/Code/pokemon-showdown",
        ],
        cwd=repo,
    )
elif re.search(r"generate-tm-data[.]cs$", file_path):
    print("Regenerating tm-data.json...", flush=True)
    subprocess.run(
        ["dotnet", "run", "tools/generate-tm-data.cs"],
        cwd=repo,
    )
