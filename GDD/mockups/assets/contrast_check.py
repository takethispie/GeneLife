#!/usr/bin/env python3
"""AC-6: contrast assertion for the "Civic Paper" tokens.

Reads the token values straight out of `assets/mockups.css` (no second copy of the
palette to drift) and checks every text-on-surface pair the design language permits
against its WCAG 2.1 requirement. Exit code 1 means the language no longer meets its
own rule — which is what a client build should fail on, not what a designer should
re-negotiate.

Usage:  python3 GDD/mockups/assets/contrast_check.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

CSS = Path(__file__).with_name("mockups.css")
# (foreground token, background token, minimum ratio, why)
PAIRS = [
    ("--ink", "--paper", 7.0, "body text"),
    ("--ink-2", "--paper", 7.0, "secondary body text"),
    ("--ink-2", "--paper-2", 4.5, "secondary text on recessed surface"),
    ("--ink-3", "--paper", 4.5, "labels / tertiary"),
    ("--ink-3", "--paper-2", 4.5, "labels on recessed surface"),
    ("--money", "--paper", 4.5, "money figures"),
    ("--accent", "--paper", 4.5, "accent / links"),
    ("--body", "--paper", 4.5, "needs domain"),
    ("--work", "--paper", 4.5, "work domain"),
    ("--social", "--paper", 4.5, "social domain"),
    ("--legacy", "--paper", 4.5, "legacy domain"),
    ("--warn", "--paper", 4.5, "warning text"),
    ("--ok", "--paper", 4.5, "success text"),
    ("--paper", "--ink", 4.5, "text on primary button / dark chip"),
    ("--paper", "--accent", 4.5, "text on accent button"),
]


def tokens() -> dict[str, str]:
    css = CSS.read_text(encoding="utf-8")
    block = re.search(r":root\s*\{(.*?)\n\}", css, re.S)
    if not block:
        raise SystemExit("contrast_check: could not find :root in mockups.css")
    found = dict(re.findall(r"(--[a-z0-9-]+)\s*:\s*(#[0-9A-Fa-f]{6})", block.group(1)))
    missing = {t for pair in PAIRS for t in pair[:2]} - found.keys()
    if missing:
        raise SystemExit(f"contrast_check: tokens not found: {sorted(missing)}")
    return found


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
    table = tokens()
    failures = 0
    for fg_token, bg_token, minimum, why in PAIRS:
        got = ratio(table[fg_token], table[bg_token])
        ok = got >= minimum
        failures += 0 if ok else 1
        verdict = "PASS" if ok else "FAIL"
        print(f"{verdict}  {why:34s} {fg_token:9s} on {bg_token:9s} "
              f"{table[fg_token]} / {table[bg_token]}  {got:5.2f}:1  (min {minimum})")
    print(f"\n{len(PAIRS) - failures}/{len(PAIRS)} pairs pass")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())