#!/usr/bin/env python3
"""AC-6: contrast assertion for the "Civic Paper" tokens — designer's local loop.

The *rule* (which pairs must meet which ratio) lives in `contrast-pairs.json`; the *colours*
live in `mockups.css`. This script reads both and prints the verdict the CI gate
(`Genelife.Domain.Tests/Docs/ContrastTokenTests.cs`) computes from the same two inputs, so a
local pass and a merge gate cannot disagree — there is no second copy of the pair table.

Exit code 1 means the language no longer meets its own rule: that is a palette to fix, not a
threshold to relax.

Usage:  python3 GDD/mockups/assets/contrast_check.py
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
CSS = HERE / "mockups.css"
RULES = HERE / "contrast-pairs.json"


def tokens() -> dict[str, str]:
    css = CSS.read_text(encoding="utf-8")
    block = re.search(r":root\s*\{(.*?)\n\}", css, re.S)
    if not block:
        raise SystemExit(f"contrast_check: could not find a :root block in {CSS.name}")
    found = dict(re.findall(r"(--[a-z0-9-]+)\s*:\s*(#[0-9A-Fa-f]{6})", block.group(1)))
    if not found:
        raise SystemExit(f"contrast_check: no colour tokens found in {CSS.name}")
    return found


def rules() -> list[dict]:
    data = json.loads(RULES.read_text(encoding="utf-8"))
    pairs = data.get("pairs") or []
    if not pairs:
        raise SystemExit(f"contrast_check: {RULES.name} declares no pairs")
    return pairs


def _channel(value: float) -> float:
    return value / 12.92 if value <= 0.03928 else ((value + 0.055) / 1.055) ** 2.4


def luminance(hex_colour: str) -> float:
    r, g, b = (int(hex_colour[i:i + 2], 16) / 255 for i in (1, 3, 5))
    return 0.2126 * _channel(r) + 0.7152 * _channel(g) + 0.0722 * _channel(b)


def ratio(fg: str, bg: str) -> float:
    a, b = luminance(fg), luminance(bg)
    hi, lo = max(a, b), min(a, b)
    return (hi + 0.05) / (lo + 0.05)


def main() -> int:
    table, pairs = tokens(), rules()
    missing = sorted({t for p in pairs for t in (p["foreground"], p["background"])} - table.keys())
    if missing:
        raise SystemExit(f"contrast_check: {CSS.name} does not declare {', '.join(missing)}")

    failures = 0
    for pair in pairs:
        fg, bg = pair["foreground"], pair["background"]
        got = ratio(table[fg], table[bg])
        minimum = float(pair["minimum"])
        ok = got >= minimum
        failures += 0 if ok else 1
        print(f"{'PASS' if ok else 'FAIL'}  {pair['use']:34s} {fg:9s} on {bg:9s} "
              f"{table[fg]} / {table[bg]}  {got:5.2f}:1  (min {minimum})")

    print(f"\n{len(pairs) - failures}/{len(pairs)} pairs pass")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
