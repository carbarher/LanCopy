#!/usr/bin/env python3
"""Generate a TXT list of novels (<= 1900) from Wikidata.

Output format is one entry per line:
    Autor - Título

Designed to be imported into SlskDown via the '📄 BUSCAR OBRAS (TXT)' button.

Notes:
- 'Importance' is approximated by the number of Wikidata sitelinks.
- If you need Spanish titles "as published", enable --require-eswiki-title.
- Titles/authors are requested in Spanish first (fallback to English) unless
  Spanish title is enforced.
"""

from __future__ import annotations

import argparse
import json
import time
import unicodedata
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Iterable, Optional


WIKIDATA_SPARQL_ENDPOINT: str = "https://query.wikidata.org/sparql"
USER_AGENT: str = (
    "SlskDown/1.0 (generate_universal_novels_pre1900_wikidata.py; "
    "+https://query.wikidata.org/)"
)


@dataclass(frozen=True)
class NovelRow:
    title: str
    author: str
    year: Optional[int]
    sitelinks: int


def _normalize_key(text: str) -> str:
    """Normalize a string for deduping.

    - lowercase
    - remove accents
    - replace punctuation with spaces
    - collapse whitespace
    """

    text = text.strip().lower()
    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")

    cleaned_chars: list[str] = []
    for ch in text:
        if ch.isalnum() or ch.isspace():
            cleaned_chars.append(ch)
        else:
            cleaned_chars.append(" ")

    collapsed = " ".join("".join(cleaned_chars).split())
    return collapsed


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


def _run_sparql(query: str, retries: int = 3, sleep_s: float = 1.5) -> dict:
    params = {
        "format": "json",
        "query": query,
    }
    url = f"{WIKIDATA_SPARQL_ENDPOINT}?{urllib.parse.urlencode(params)}"

    last_err: Optional[Exception] = None
    for attempt in range(retries):
        try:
            data = _http_get(url)
            return json.loads(data.decode("utf-8"))
        except Exception as exc:  # noqa: BLE001
            last_err = exc
            if attempt < retries - 1:
                time.sleep(sleep_s * (attempt + 1))

    raise RuntimeError(
        f"SPARQL request failed after {retries} tries: {last_err}"
    )


def fetch_novels(
    limit: int,
    require_eswiki_title: bool,
    prefer_eswiki_title: bool,
    require_original_spanish: bool,
    allow_english_fallback: bool,
) -> list[NovelRow]:
    # Q8261 = novel
    # P31 = instance of
    # P279 = subclass of
    # P577 = publication date
    # P571 = inception
    # P50 = author
    # wikibase:sitelinks = sitelinks count
    # P364 = original language of work (Spanish is Q1321)
    language_block = (
        "?work wdt:P364 wd:Q1321 ."
        if require_original_spanish
        else ""
    )

    # Many novels are not typed as instance-of "novel"; they are tagged via
    # genre (P136). We accept either.
    novel_criteria = """
  { ?work wdt:P31/wdt:P279* wd:Q8261 . }
  UNION
  { ?work wdt:P136/wdt:P279* wd:Q8261 . }
""".strip()

    want_eswiki = bool(require_eswiki_title or prefer_eswiki_title)
    title_block = (
        """
  OPTIONAL {
    ?esArticle schema:about ?work ;
               schema:isPartOf <https://es.wikipedia.org/> .
  }
""".strip()
        if want_eswiki
        else ""
    )

    title_select = (
        "?esArticle ?workLabelEs ?workLabelEn"
        if want_eswiki
        else "?workLabelEs ?workLabelEn"
    )

    eswiki_required_filter = (
        "FILTER(BOUND(?esArticle))"
        if require_eswiki_title
        else ""
    )

    # Even when forcing Spanish title via eswiki, keep author label fallback.
    label_languages = "es,en"

    candidate_limit = max(limit * 30, 10000)

    query = f"""
PREFIX schema: <http://schema.org/>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
SELECT ?work {title_select} ?authorLabelEs ?authorLabelEn
       ?authorLabel ?year ?sitelinks WHERE {{
  {novel_criteria}

  {language_block}

  OPTIONAL {{ ?work wdt:P577 ?publicationDate . }}
  OPTIONAL {{ ?work wdt:P571 ?inceptionDate . }}
  BIND(COALESCE(?publicationDate, ?inceptionDate) AS ?date)
  FILTER(BOUND(?date))
  BIND(YEAR(?date) AS ?year)
  FILTER(?year <= 1900)

  OPTIONAL {{
    ?work wdt:P50 ?author .
    FILTER(STRSTARTS(STR(?author), "http://www.wikidata.org/entity/"))
  }}

  OPTIONAL {{
    ?author rdfs:label ?authorLabelEs .
    FILTER(LANG(?authorLabelEs) = "es")
  }}

  OPTIONAL {{
    ?author rdfs:label ?authorLabelEn .
    FILTER(LANG(?authorLabelEn) = "en")
  }}

  OPTIONAL {{
    ?work rdfs:label ?workLabelEs .
    FILTER(LANG(?workLabelEs) = "es")
  }}

  OPTIONAL {{
    ?work rdfs:label ?workLabelEn .
    FILTER(LANG(?workLabelEn) = "en")
  }}

  {title_block}

  {eswiki_required_filter}

  ?work wikibase:sitelinks ?sitelinks .

  SERVICE wikibase:label {{
    bd:serviceParam wikibase:language "{label_languages}".
  }}
}}
ORDER BY DESC(?sitelinks) DESC(?year)
LIMIT {candidate_limit}
""".strip()

    raw = _run_sparql(query)
    bindings = raw.get("results", {}).get("bindings", [])

    rows: list[NovelRow] = []
    for b in bindings:
        work_label_es = b.get("workLabelEs", {}).get("value", "").strip()
        work_label_en = b.get("workLabelEn", {}).get("value", "").strip()
        es_article = b.get("esArticle", {}).get("value", "").strip()

        author_label_es = b.get("authorLabelEs", {}).get("value", "").strip()
        author_label_en = b.get("authorLabelEn", {}).get("value", "").strip()

        title: str = ""
        if require_eswiki_title and es_article:
            title = _title_from_eswiki_url(es_article)

        # Default behavior for Spanish titles: prefer Wikidata Spanish label.
        if not title:
            title = work_label_es

        # If Spanish label is missing, optionally use eswiki page title.
        if not title and prefer_eswiki_title and es_article:
            title = _title_from_eswiki_url(es_article)

        if not title and allow_english_fallback:
            title = work_label_en

        if title.startswith("http://") or title.startswith("https://"):
            title = ""

        author = author_label_es or author_label_en
        if not author:
            author = b.get("authorLabel", {}).get("value", "").strip()
        year_str = b.get("year", {}).get("value", "")
        sitelinks_str = b.get("sitelinks", {}).get("value", "0")

        if not title:
            continue

        year = _safe_int(year_str)
        sitelinks = _safe_int(sitelinks_str) or 0

        if (
            not author
            or author.startswith("http://")
            or author.startswith("https://")
            or (author.startswith("Q") and author[1:].isdigit())
        ):
            author = "Desconocido"

        rows.append(
            NovelRow(
                title=title,
                author=author,
                year=year,
                sitelinks=sitelinks,
            )
        )

    return rows


def _title_from_eswiki_url(url: str) -> str:
    url = url.strip()
    if not url:
        return ""

    # Handle canonical /wiki/ titles.
    if "/wiki/" in url:
        part = url.split("/wiki/", 1)[1]
    else:
        part = url

    part = part.split("?", 1)[0]
    part = part.split("#", 1)[0]
    part = part.replace("_", " ")
    part = urllib.parse.unquote(part)
    return part.strip()


def dedupe_and_format(rows: Iterable[NovelRow], limit: int) -> list[str]:
    seen: set[str] = set()
    output: list[str] = []

    for row in rows:
        line = f"{row.author} - {row.title}".strip()
        key = _normalize_key(line)
        if not key or key in seen:
            continue

        seen.add(key)
        output.append(line)
        if len(output) >= limit:
            break

    return output


def write_lines(lines: list[str], out_path: str) -> None:
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        for line in lines:
            f.write(line)
            f.write("\n")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Generate a TXT list of important novels (<=1900) from Wikidata "
            "(Spanish labels)."
        )
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=1000,
        help="Number of lines to generate (default: 1000).",
    )
    eswiki_group = parser.add_mutually_exclusive_group()
    eswiki_group.add_argument(
        "--require-eswiki-title",
        action="store_true",
        default=False,
        help=(
            "Require Spanish title via eswiki sitelink (strict; may reduce "
            "results)."
        ),
    )
    eswiki_group.add_argument(
        "--prefer-eswiki-title",
        action="store_true",
        default=False,
        help=(
            "Prefer Spanish title via eswiki sitelink, but fallback to "
            "Spanish label if missing (recommended)."
        ),
    )
    parser.add_argument(
        "--allow-english-fallback",
        action="store_true",
        default=False,
        help=(
            "Allow English title fallback if Spanish title is missing. "
            "(Not recommended.)"
        ),
    )
    parser.add_argument(
        "--require-original-spanish",
        action="store_true",
        default=False,
        help=(
            "Require original language Spanish (P364=Spanish). "
            "WARNING: this excludes translated world classics."
        ),
    )
    parser.add_argument(
        "--out",
        type=str,
        default="novelas_1000_pre1900_wikidata.txt",
        help=(
            "Output TXT path (default: novelas_1000_pre1900_wikidata.txt)."
        ),
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    if args.limit <= 0:
        raise SystemExit("--limit must be > 0")

    require_eswiki_title = bool(args.require_eswiki_title)
    prefer_eswiki_title = bool(getattr(args, "prefer_eswiki_title", False))
    require_original_spanish = bool(args.require_original_spanish)
    allow_english_fallback = bool(args.allow_english_fallback)

    rows = fetch_novels(
        args.limit,
        require_eswiki_title=require_eswiki_title,
        prefer_eswiki_title=prefer_eswiki_title,
        require_original_spanish=require_original_spanish,
        allow_english_fallback=allow_english_fallback,
    )
    lines = dedupe_and_format(rows, limit=args.limit)

    if not lines:
        raise SystemExit(
            "No results produced. Try disabling strict flags or try again "
            "later."
        )

    write_lines(lines, args.out)

    print(f"Wrote {len(lines)} lines to: {args.out}")


if __name__ == "__main__":
    main()
