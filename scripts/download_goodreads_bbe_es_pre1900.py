#!/usr/bin/env python3
"""Download a public Goodreads-like dataset and filter Spanish books pre-1900.

This script downloads the GoodReads Best Books Ever dataset (CSV) from GitHub
and writes a filtered CSV with only Spanish-language rows and publication year
<= 1900.

It is meant to produce an input for:
  scripts/generate_universal_novels_pre1900_multisource.py --goodreads-es-csv
  ...

Output files are placed next to this script by default.
"""

from __future__ import annotations

import argparse
import csv
import re
import urllib.request
from pathlib import Path
from typing import Optional


DEFAULT_URL: str = (
    "https://raw.githubusercontent.com/zygmuntz/goodbooks-10k/"
    "master/books.csv"
)

SPANISH_CODES = {"spa", "es", "es-es", "es_419", "es-419", "es_mx", "es-ar"}


def _extract_year(text: str) -> Optional[int]:
    text = (text or "").strip()
    if not text:
        return None
    match = re.search(r"\b(1[0-9]{3}|1900)\b", text)
    if not match:
        return None
    try:
        return int(match.group(1))
    except Exception:
        return None


def _looks_spanish_language(lang: str) -> bool:
    lang = (lang or "").strip().lower()
    if not lang:
        return False
    if lang in SPANISH_CODES:
        return True
    if lang.startswith("es"):
        return True
    if "spa" in lang:
        return True
    if "spanish" in lang:
        return True
    if "espa" in lang:  # español / espanol
        return True
    return False


def _download(url: str, out_path: Path) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=120) as resp:
        out_path.write_bytes(resp.read())


def _filter(
    *,
    in_path: Path,
    out_path: Path,
    min_year: int,
    max_year: int,
) -> int:
    out_path.parent.mkdir(parents=True, exist_ok=True)

    with in_path.open("r", encoding="utf-8", newline="") as fin:
        reader = csv.DictReader(fin)
        fieldnames = reader.fieldnames
        if not fieldnames:
            raise SystemExit("CSV sin cabecera (fieldnames)")

        with out_path.open("w", encoding="utf-8", newline="") as fout:
            writer = csv.DictWriter(fout, fieldnames=fieldnames)
            writer.writeheader()

            kept = 0
            for row in reader:
                if not isinstance(row, dict):
                    continue

                lang = (
                    row.get("language_code")
                    or row.get("language")
                    or row.get("lang")
                    or ""
                )
                if not _looks_spanish_language(str(lang)):
                    continue

                year = (
                    _extract_year(
                        str(row.get("original_publication_year", ""))
                    )
                    or _extract_year(str(row.get("firstPublishDate", "")))
                    or _extract_year(str(row.get("publishDate", "")))
                    or _extract_year(str(row.get("first_publish_year", "")))
                    or _extract_year(str(row.get("year", "")))
                )
                if year is None:
                    continue
                if year < min_year or year > max_year:
                    continue

                writer.writerow(row)
                kept += 1

    return kept


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", type=str, default=DEFAULT_URL)
    parser.add_argument(
        "--raw-out",
        type=str,
        default=str(Path(__file__).with_name("goodreads_goodbooks10k.csv")),
    )
    parser.add_argument(
        "--es-out",
        type=str,
        default=str(Path(__file__).with_name("goodreads_books_es.csv")),
    )
    parser.add_argument("--min-year", type=int, default=0)
    parser.add_argument("--max-year", type=int, default=1900)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    raw_out = Path(args.raw_out)
    es_out = Path(args.es_out)

    if not raw_out.exists():
        print(f"Descargando dataset: {args.url}")
        _download(args.url, raw_out)
        print(f"Descargado: {raw_out}")
    else:
        print(f"Ya existe (no se descarga): {raw_out}")

    kept = _filter(
        in_path=raw_out,
        out_path=es_out,
        min_year=int(args.min_year),
        max_year=int(args.max_year),
    )
    print(
        f"Filtrado OK. Filas en español <= {args.max_year}: {kept}"
    )
    print(f"Salida: {es_out}")


if __name__ == "__main__":
    main()
