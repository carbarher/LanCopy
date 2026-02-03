#!/usr/bin/env python3
"""Generate a TXT list of Spanish Project Gutenberg novels (<= 1900).

This script uses the local `gutenberg_metadata.csv` to discover Gutenberg ebook
IDs (language == 'es'), then queries Wikidata to:

- confirm the work is a novel
- get publication year and filter <= 1900
- retrieve title/author labels in Spanish (fallback to English)

Output format (one per line):
    Autor - Título

Designed to be imported into SlskDown via the '📄 BUSCAR OBRAS (TXT)' button.

Notes:
- "Importance" is approximated by Wikidata sitelinks count.
- Many "important" universal novels may not exist in Spanish on Gutenberg; if
  the result count is low, use the Wikidata-only generator or relax filters.
"""

from __future__ import annotations

import argparse
import csv
import json
import time
import unicodedata
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional


WIKIDATA_SPARQL_ENDPOINT: str = "https://query.wikidata.org/sparql"
USER_AGENT: str = (
    "SlskDown/1.0 (generate_universal_novels_pre1900_gutenberg_es.py; "
    "+https://query.wikidata.org/)"
)


@dataclass(frozen=True)
class NovelRow:
    title: str
    author: str
    year: Optional[int]
    sitelinks: int
    pg_id: str


def _normalize_key(text: str) -> str:
    text = text.strip().lower()
    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")

    cleaned_chars: list[str] = []
    for ch in text:
        if ch.isalnum() or ch.isspace():
            cleaned_chars.append(ch)
        else:
            cleaned_chars.append(" ")

    return " ".join("".join(cleaned_chars).split())


def _safe_int(value: str) -> Optional[int]:
    try:
        return int(value)
    except Exception:
        return None


def _http_get(url: str, timeout_s: int = 60) -> bytes:
    req = urllib.request.Request(
        url,
        method="GET",
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "application/sparql-results+json",
        },
    )

    with urllib.request.urlopen(req, timeout=timeout_s) as resp:
        return resp.read()


def _http_post_form(
    url: str,
    *,
    data: dict[str, str],
    timeout_s: int = 60,
) -> bytes:
    body = urllib.parse.urlencode(data).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "application/sparql-results+json",
            "Content-Type": "application/x-www-form-urlencoded; charset=utf-8",
        },
    )

    with urllib.request.urlopen(req, timeout=timeout_s) as resp:
        return resp.read()


def _run_sparql(query: str, retries: int = 3, sleep_s: float = 1.5) -> dict:
    params = {
        "format": "json",
        "query": query,
    }
    url = f"{WIKIDATA_SPARQL_ENDPOINT}?{urllib.parse.urlencode(params)}"

    use_post = len(url) > 1800 or len(query) > 1500

    last_err: Optional[Exception] = None
    for attempt in range(retries):
        try:
            if use_post:
                data = _http_post_form(WIKIDATA_SPARQL_ENDPOINT, data=params)
            else:
                data = _http_get(url)
            return json.loads(data.decode("utf-8"))
        except Exception as exc:  # noqa: BLE001
            last_err = exc
            if attempt < retries - 1:
                time.sleep(sleep_s * (attempt + 1))

    raise RuntimeError(
        f"SPARQL request failed after {retries} tries: {last_err}"
    )


def read_gutenberg_ids(csv_path: Path, language: str) -> list[str]:
    if not csv_path.exists():
        raise FileNotFoundError(str(csv_path))

    ids: list[str] = []
    with csv_path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        if reader.fieldnames is None:
            return []

        for row in reader:
            if (row.get("Type") or "").strip() != "Text":
                continue

            if language.lower() != "any":
                row_lang = (row.get("Language") or "").strip().lower()
                if row_lang != language.lower():
                    continue

            etext_number = (row.get("Etext Number") or "").strip()
            if not etext_number:
                continue

            if _safe_int(etext_number) is None:
                continue

            ids.append(etext_number)

    # Deduplicate while preserving order
    seen: set[str] = set()
    output: list[str] = []
    for pg_id in ids:
        if pg_id not in seen:
            seen.add(pg_id)
            output.append(pg_id)

    return output


def batched(items: list[str], batch_size: int) -> Iterable[list[str]]:
    for i in range(0, len(items), batch_size):
        yield items[i:i + batch_size]


def fetch_novels_from_wikidata(
    pg_ids: list[str],
    batch_size: int,
    require_novel: bool,
    allow_missing_year: bool,
) -> list[NovelRow]:
    all_rows: list[NovelRow] = []

    for chunk in batched(pg_ids, batch_size=batch_size):
        # P2034 (Project Gutenberg ebook ID) values are typically typed as
        # xsd:string in Wikidata; matching requires term equality.
        values = " ".join(
            f'"{pg_id}"^^xsd:string' for pg_id in chunk
        )

        # Q8261 = novel
        # P2034 = Project Gutenberg ebook ID
        # P577 = publication date
        # P571 = inception
        # P50 = author

        novel_block = ""
        if require_novel:
            # Some works are tagged as novels via genre (P136) rather than
            # instance-of (P31).
            novel_block = """
  { ?work wdt:P31/wdt:P279* wd:Q8261 . }
  UNION
  { ?work wdt:P136/wdt:P279* wd:Q8261 . }
""".strip()

        if allow_missing_year:
            year_block = """
  OPTIONAL { ?work wdt:P577 ?publicationDate . }
  OPTIONAL { ?work wdt:P571 ?inceptionDate . }
  BIND(COALESCE(?publicationDate, ?inceptionDate) AS ?date)
  BIND(IF(BOUND(?date), YEAR(?date), -1) AS ?year)
  FILTER(?year = -1 || ?year <= 1900)
""".strip()
        else:
            year_block = """
  OPTIONAL { ?work wdt:P577 ?publicationDate . }
  OPTIONAL { ?work wdt:P571 ?inceptionDate . }
  BIND(COALESCE(?publicationDate, ?inceptionDate) AS ?date)
  FILTER(BOUND(?date))
  BIND(YEAR(?date) AS ?year)
  FILTER(?year <= 1900)
""".strip()
        query = f"""
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
SELECT ?pgId ?workLabel ?authorLabel ?year ?sitelinks WHERE {{
  VALUES ?pgId {{ {values} }}
  ?work wdt:P2034 ?pgId .

  {novel_block}

  {year_block}

  OPTIONAL {{ ?work wdt:P50 ?author . }}
  ?work wikibase:sitelinks ?sitelinks .

  SERVICE wikibase:label {{ bd:serviceParam wikibase:language "es,en". }}
}}
""".strip()

        raw = _run_sparql(query)
        bindings = raw.get("results", {}).get("bindings", [])

        for b in bindings:
            title = b.get("workLabel", {}).get("value", "").strip()
            author = b.get("authorLabel", {}).get("value", "").strip()
            year_str = b.get("year", {}).get("value", "")
            sitelinks_str = b.get("sitelinks", {}).get("value", "0")
            pg_id = b.get("pgId", {}).get("value", "").strip()

            if not title or not pg_id:
                continue

            if not author:
                author = "Desconocido"

            all_rows.append(
                NovelRow(
                    title=title,
                    author=author,
                    year=_safe_int(year_str),
                    sitelinks=_safe_int(sitelinks_str) or 0,
                    pg_id=pg_id,
                )
            )

        # Be polite to the endpoint.
        time.sleep(0.2)

    return all_rows


def dedupe_sort_format(rows: list[NovelRow], limit: int) -> list[str]:
    rows_sorted = sorted(
        rows,
        key=lambda r: (
            r.sitelinks,
            r.year or -1,
            r.author.lower(),
            r.title.lower(),
        ),
        reverse=True,
    )

    seen: set[str] = set()
    output: list[str] = []

    for row in rows_sorted:
        line = f"{row.author} - {row.title}".strip()
        key = _normalize_key(line)
        if not key or key in seen:
            continue

        seen.add(key)
        output.append(line)
        if len(output) >= limit:
            break

    return output


def write_lines(lines: list[str], out_path: Path) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Generate a TXT list of Spanish Project Gutenberg novels (<=1900) "
            "using gutenberg_metadata.csv + Wikidata."
        )
    )
    parser.add_argument(
        "--csv",
        type=str,
        default="gutenberg_metadata.csv",
        help=(
            "Path to gutenberg_metadata.csv "
            "(default: gutenberg_metadata.csv)."
        ),
    )
    parser.add_argument(
        "--csv-language",
        type=str,
        default="es",
        help=(
            "Filter Gutenberg CSV by Language code (default: es). "
            "Use 'any' to include all languages."
        ),
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=1000,
        help="Number of lines to generate (default: 1000).",
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=150,
        help="Wikidata VALUES batch size (default: 150).",
    )
    parser.add_argument(
        "--require-novel",
        action="store_true",
        default=True,
        help="Require Wikidata 'novel' type (default: enabled).",
    )
    parser.add_argument(
        "--no-require-novel",
        dest="require_novel",
        action="store_false",
        help="Disable novel-type constraint (relaxes results).",
    )
    parser.add_argument(
        "--allow-missing-year",
        action="store_true",
        default=False,
        help=(
            "Allow works with missing publication year; "
            "keeps <=1900 when known."
        ),
    )
    parser.add_argument(
        "--out",
        type=str,
        default="novelas_es_pre1900_gutenberg_wikidata.txt",
        help="Output TXT path.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    csv_path = Path(args.csv)
    out_path = Path(args.out)

    if args.limit <= 0:
        raise SystemExit("--limit must be > 0")

    pg_ids = read_gutenberg_ids(csv_path, language=str(args.csv_language))
    if not pg_ids:
        raise SystemExit(
            "No Gutenberg IDs found in CSV for the requested language filter."
        )

    all_rows: list[NovelRow] = []

    def _extend(rows: list[NovelRow]) -> None:
        if rows:
            all_rows.extend(rows)

    passes: list[tuple[bool, bool]] = [
        (bool(args.require_novel), bool(args.allow_missing_year)),
        (bool(args.require_novel), True),
        (False, True),
    ]

    for require_novel, allow_missing_year in passes:
        rows = fetch_novels_from_wikidata(
            pg_ids,
            batch_size=args.batch_size,
            require_novel=require_novel,
            allow_missing_year=allow_missing_year,
        )
        _extend(rows)
        lines = dedupe_sort_format(all_rows, limit=args.limit)
        if len(lines) >= args.limit:
            break

    if not lines:
        raise SystemExit(
            "No results produced. Try increasing batch-size, or use the "
            "Wikidata-only generator."
        )

    write_lines(lines, out_path)

    print(f"Wrote {len(lines)} lines to: {out_path}")
    if str(args.csv_language).lower() == "es":
        print(
            "NOTE: This list is limited to novels that exist in Spanish on "
            "Project Gutenberg."
        )
    else:
        print(
            "NOTE: This list uses Project Gutenberg IDs as seeds; titles are "
            "pulled from Wikidata labels (es,en)."
        )


if __name__ == "__main__":
    main()
