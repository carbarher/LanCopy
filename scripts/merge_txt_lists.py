#!/usr/bin/env python3

from __future__ import annotations

import argparse
import unicodedata
from pathlib import Path


def _normalize_key(text: str) -> str:
    text = text.strip().lower()
    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")

    cleaned: list[str] = []
    for ch in text:
        if ch.isalnum() or ch.isspace():
            cleaned.append(ch)
        else:
            cleaned.append(" ")

    return " ".join("".join(cleaned).split())


def _iter_lines(path: Path) -> list[str]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    return [line.strip() for line in raw.splitlines() if line.strip()]


def _extract_title(line: str) -> str:
    parts = line.split(" - ", 1)
    if len(parts) == 2:
        return parts[1].strip()
    return line.strip()


def _looks_spanish_title(title: str) -> bool:
    t = title.strip().lower()
    if not t:
        return False

    if any(ch in t for ch in ("á", "é", "í", "ó", "ú", "ñ", "ü")):
        return True

    starters = (
        "el ",
        "la ",
        "los ",
        "las ",
        "un ",
        "una ",
        "unos ",
        "unas ",
    )
    if t.startswith(starters):
        return True

    padded = f" {t} "
    tokens = (
        " de ",
        " del ",
        " y ",
        " o ",
        " en ",
        " con ",
        " sin ",
        " para ",
        " por ",
    )
    if any(tok in padded for tok in tokens):
        return True

    return False


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Merge multiple TXT lists (one line per work) with deduplication."
        )
    )
    parser.add_argument(
        "--inputs",
        nargs="+",
        required=True,
        help="Input TXT paths (order matters; first files have priority).",
    )
    parser.add_argument(
        "--out",
        type=str,
        required=True,
        help="Output TXT path.",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=1000,
        help="Maximum number of unique lines to write (default: 1000).",
    )
    parser.add_argument(
        "--require-spanish-title",
        action="store_true",
        default=False,
        help=(
            "Keep only lines whose TITLE looks Spanish (author ignored)."
        ),
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    if args.limit <= 0:
        raise SystemExit("--limit must be > 0")

    inputs = [Path(p) for p in args.inputs]
    out_path = Path(args.out)

    for p in inputs:
        if not p.exists():
            raise FileNotFoundError(str(p))

    seen: set[str] = set()
    merged: list[str] = []

    for p in inputs:
        for line in _iter_lines(p):
            if bool(args.require_spanish_title):
                title = _extract_title(line)
                if not _looks_spanish_title(title):
                    continue
            key = _normalize_key(line)
            if not key or key in seen:
                continue
            seen.add(key)
            merged.append(line)
            if len(merged) >= args.limit:
                break
        if len(merged) >= args.limit:
            break

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text("\n".join(merged) + "\n", encoding="utf-8")
    print(f"Wrote {len(merged)} unique lines to: {out_path}")


if __name__ == "__main__":
    main()
