#!/usr/bin/env python3
"""Generate a TXT list of important novels (<= 1900) with Spanish titles.

This script uses Wikidata as the primary source and optionally enriches missing
Spanish titles via Open Library.

Output format is one entry per line:
    Autor - Título
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import time
import unicodedata
import urllib.parse
import urllib.request
import urllib.error
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Optional


PROGRESS_ENABLED: bool = True


def _progress(message: str) -> None:
    if not PROGRESS_ENABLED:
        return
    print(message, flush=True)


WIKIDATA_SPARQL_ENDPOINT: str = "https://query.wikidata.org/sparql"
WIKIDATA_API_ENDPOINT: str = "https://www.wikidata.org/w/api.php"
OPENLIBRARY_SEARCH_ENDPOINT: str = "https://openlibrary.org/search.json"
OPENLIBRARY_WORKS_ENDPOINT: str = "https://openlibrary.org/works"
BNE_RESOURCE_ENDPOINT: str = "https://datos.bne.es/resource"
WIKIPEDIA_EN_API_ENDPOINT: str = "https://en.wikipedia.org/w/api.php"
USER_AGENT: str = (
    "SlskDown/1.0 (generate_universal_novels_pre1900_multisource.py; "
    "+https://query.wikidata.org/)"
)


@dataclass(frozen=True)
class CandidateRow:
    work_uri: str
    title_es: str
    title_en: str
    es_article: str
    en_article: str
    author_es: str
    author_en: str
    openlibrary_id: str
    bne_id: str
    year: Optional[int]
    sitelinks: int


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


def _normalize_display_text(text: str) -> str:
    text = text.strip()
    if not text:
        return ""

    text = re.sub(r"\s*\([^)]*\)", "", text)
    return " ".join(text.split()).strip()


def _extract_p950_bne_id(claims: Any) -> str:
    if not isinstance(claims, dict):
        return ""
    p950 = claims.get("P950")
    if not isinstance(p950, list):
        return ""
    for stmt in p950:
        if not isinstance(stmt, dict):
            continue
        mainsnak = stmt.get("mainsnak", {})
        if not isinstance(mainsnak, dict):
            continue
        datavalue = mainsnak.get("datavalue", {})
        if not isinstance(datavalue, dict):
            continue
        value = datavalue.get("value")
        if isinstance(value, str):
            cleaned = value.strip()
            if cleaned:
                return cleaned
    return ""


def _extract_monolingual_text_es(claims: Any, pid: str) -> str:
    if not isinstance(claims, dict):
        return ""
    stmts = claims.get(pid)
    if not isinstance(stmts, list):
        return ""

    for stmt in stmts:
        if not isinstance(stmt, dict):
            continue
        mainsnak = stmt.get("mainsnak", {})
        if not isinstance(mainsnak, dict):
            continue
        datavalue = mainsnak.get("datavalue", {})
        if not isinstance(datavalue, dict):
            continue
        value = datavalue.get("value")
        if not isinstance(value, dict):
            continue
        text = str(value.get("text", "")).strip()
        lang = str(value.get("language", "")).strip().lower()
        if text and lang == "es":
            return text
    return ""


def _extract_p648_openlibrary_id(claims: Any) -> str:
    if not isinstance(claims, dict):
        return ""
    p648 = claims.get("P648")
    if not isinstance(p648, list):
        return ""
    for stmt in p648:
        if not isinstance(stmt, dict):
            continue
        mainsnak = stmt.get("mainsnak", {})
        if not isinstance(mainsnak, dict):
            continue
        datavalue = mainsnak.get("datavalue", {})
        if not isinstance(datavalue, dict):
            continue
        value = datavalue.get("value")
        if isinstance(value, str):
            return value.strip()
    return ""


def _extract_alias_title_es(entity: Any) -> str:
    if not isinstance(entity, dict):
        return ""
    aliases = entity.get("aliases", {})
    if not isinstance(aliases, dict):
        return ""
    es_aliases = aliases.get("es")
    if not isinstance(es_aliases, list):
        return ""
    for a in es_aliases:
        if not isinstance(a, dict):
            continue
        value = str(a.get("value", "")).strip()
        if value:
            return value
    return ""


def _http_get(url: str, timeout_s: int = 60) -> bytes:
    req = urllib.request.Request(
        url,
        method="GET",
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "application/json",
        },
    )
    with urllib.request.urlopen(req, timeout=timeout_s) as resp:
        return resp.read()


def _is_transient_request_error(exc: Exception) -> bool:
    if isinstance(exc, urllib.error.HTTPError):
        return exc.code in {429, 500, 502, 503, 504}
    if isinstance(exc, urllib.error.URLError):
        return True
    if isinstance(exc, TimeoutError):
        return True
    return False


def _run_wikidata_api(
    params: dict[str, str],
    *,
    retries: int = 3,
    sleep_s: float = 1.5,
    timeout_s: int = 60,
    tag: str = "",
) -> dict[str, Any]:
    last_err: Optional[Exception] = None
    base_url = WIKIDATA_API_ENDPOINT

    for attempt in range(retries):
        try:
            if tag:
                if attempt == 0:
                    _progress(f"{tag}: consultando Wikidata API...")
                else:
                    _progress(
                        f"{tag}: reintento {attempt + 1}/{retries}..."
                    )
            url = f"{base_url}?{urllib.parse.urlencode(params)}"
            raw = _http_get(url, timeout_s=timeout_s)
            return json.loads(raw.decode("utf-8"))
        except Exception as exc:  # noqa: BLE001
            last_err = exc
            if attempt < retries - 1:
                multiplier = 1.0
                if isinstance(exc, urllib.error.HTTPError):
                    if exc.code in (429, 503, 504):
                        multiplier = 6.0
                time.sleep(sleep_s * (attempt + 1) * multiplier)

    raise RuntimeError(
        f"Wikidata API request failed after {retries} tries: {last_err}"
    )


def _qid_from_entity_uri(uri: str) -> str:
    uri = uri.strip()
    if not uri:
        return ""
    if uri.startswith("http://") or uri.startswith("https://"):
        return uri.rsplit("/", 1)[-1]
    return uri


def _extract_p1476_title_es(claims: Any) -> str:
    if not isinstance(claims, dict):
        return ""
    p1476 = claims.get("P1476")
    if not isinstance(p1476, list):
        return ""

    for stmt in p1476:
        if not isinstance(stmt, dict):
            continue
        mainsnak = stmt.get("mainsnak", {})
        if not isinstance(mainsnak, dict):
            continue
        datavalue = mainsnak.get("datavalue", {})
        if not isinstance(datavalue, dict):
            continue
        value = datavalue.get("value")
        if not isinstance(value, dict):
            continue
        text = str(value.get("text", "")).strip()
        lang = str(value.get("language", "")).strip().lower()
        if text and lang == "es":
            return text

    return ""


def _http_post_form(
    url: str,
    form: dict[str, str],
    *,
    timeout_s: int,
    accept: str,
) -> bytes:
    data = urllib.parse.urlencode(form).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        method="POST",
        headers={
            "User-Agent": USER_AGENT,
            "Accept": accept,
            "Content-Type": "application/x-www-form-urlencoded",
        },
    )
    with urllib.request.urlopen(req, timeout=timeout_s) as resp:
        return resp.read()


def _run_sparql(
    query: str,
    retries: int = 6,
    sleep_s: float = 1.5,
    timeout_s: int = 600,
    tag: str = "",
) -> dict[str, Any]:
    form = {
        "format": "json",
        "query": query,
    }

    last_err: Optional[Exception] = None
    for attempt in range(retries):
        try:
            if tag:
                if attempt == 0:
                    _progress(f"{tag}: consultando Wikidata...")
                else:
                    _progress(
                        f"{tag}: reintento {attempt + 1}/{retries}..."
                    )
            data = _http_post_form(
                WIKIDATA_SPARQL_ENDPOINT,
                form,
                timeout_s=timeout_s,
                accept="application/sparql-results+json",
            )
            return json.loads(data.decode("utf-8"))
        except Exception as exc:  # noqa: BLE001
            last_err = exc
            if attempt < retries - 1:
                multiplier = 1.0
                if isinstance(exc, urllib.error.HTTPError):
                    if exc.code in (429, 503, 504):
                        multiplier = 4.0
                time.sleep(sleep_s * (attempt + 1) * multiplier)

    raise RuntimeError(
        f"SPARQL request failed after {retries} tries: {last_err}"
    )


def _title_from_eswiki_url(url: str) -> str:
    url = url.strip()
    if not url:
        return ""

    if "/wiki/" in url:
        part = url.split("/wiki/", 1)[1]
    else:
        part = url

    part = part.split("?", 1)[0]
    part = part.split("#", 1)[0]
    part = part.replace("_", " ")
    part = urllib.parse.unquote(part)
    title = part.strip()

    if title.startswith("http://") or title.startswith("https://"):
        return ""

    return title


def _url_from_wikimedia_sitelink(site: str, title: str) -> str:
    title = title.strip()
    if not title:
        return ""
    encoded = urllib.parse.quote(
        title.replace(" ", "_"),
        safe="()'!~*.-_",
    )

    base_by_site = {
        "eswiki": "https://es.wikipedia.org/wiki/",
        "eswikisource": "https://es.wikisource.org/wiki/",
        "eswikibooks": "https://es.wikibooks.org/wiki/",
        "eswikiquote": "https://es.wikiquote.org/wiki/",
        "enwiki": "https://en.wikipedia.org/wiki/",
    }
    base = base_by_site.get(site, "")
    if not base:
        return ""
    return f"{base}{encoded}"


def fetch_candidates(
    *,
    limit: int,
    min_sitelinks: int,
    include_en_labels: bool,
    internal_sleep_s: float,
    candidate_pool: int,
) -> list[CandidateRow]:
    novel_criteria = """
  { ?work wdt:P31/wdt:P279* wd:Q8261 . }
  UNION
  { ?work wdt:P136/wdt:P279* wd:Q8261 . }
""".strip()

    if candidate_pool > 0:
        target_pool = int(candidate_pool)
    else:
        target_pool = max(limit * 5, 8000)
    min_required = min(target_pool, max(limit, 500))

    thresholds = [int(min_sitelinks)]
    for t in (15, 10, 5, 0):
        if t < min_sitelinks:
            thresholds.append(t)

    best: list[tuple[str, Optional[int], int]] = []
    for t in thresholds:
        _progress(f"Wikidata IDs: intentando min_sitelinks={t}")
        candidates = _fetch_candidate_ids(
            novel_criteria=novel_criteria,
            target_pool=target_pool,
            min_sitelinks=t,
            internal_sleep_s=internal_sleep_s,
        )
        if len(candidates) > len(best):
            best = candidates
        if len(candidates) >= min_required:
            break
        _progress(
            f"Wikidata IDs: solo {len(candidates)} candidatos con "
            f"min_sitelinks={t}; bajando umbral..."
        )
        time.sleep(max(0.0, internal_sleep_s) * 4.0)

    candidates = best

    _progress(f"Wikidata: {len(candidates)} candidatos base (IDs) obtenidos")

    rows = _fetch_candidate_metadata(
        candidates,
        include_en_labels=include_en_labels,
        internal_sleep_s=internal_sleep_s,
    )
    rows.sort(key=lambda r: (-(r.sitelinks or 0), -(r.year or -9999)))
    _progress(f"Wikidata: {len(rows)} candidatos con metadatos")
    return rows


def _fetch_candidate_ids(
    *,
    novel_criteria: str,
    target_pool: int,
    min_sitelinks: int,
    internal_sleep_s: float,
) -> list[tuple[str, Optional[int], int]]:
    rows: list[tuple[str, Optional[int], int]] = []
    offset = 0
    page_size = 500

    while len(rows) < target_pool:
        query_limit = min(page_size, target_pool - len(rows))
        query = f"""
SELECT ?work ?year ?sitelinks WHERE {{
  {novel_criteria}

  OPTIONAL {{ ?work wdt:P577 ?publicationDate . }}
  OPTIONAL {{ ?work wdt:P571 ?inceptionDate . }}
  BIND(COALESCE(?publicationDate, ?inceptionDate) AS ?date)
  FILTER(BOUND(?date))
  BIND(YEAR(?date) AS ?year)
  FILTER(?year <= 1900)

  ?work wikibase:sitelinks ?sitelinks .
  FILTER(?sitelinks >= {min_sitelinks})
}}
ORDER BY DESC(?sitelinks) DESC(?year)
LIMIT {query_limit}
OFFSET {offset}
""".strip()

        try:
            raw = _run_sparql(query, retries=6, sleep_s=2.5, timeout_s=300)
        except RuntimeError as exc:
            _progress(f"Wikidata IDs: fallo recuperable: {exc}")
            break
        bindings = raw.get("results", {}).get("bindings", [])
        if not bindings:
            break

        for b in bindings:
            work_uri = b.get("work", {}).get("value", "").strip()
            if not work_uri:
                continue
            year = _safe_int(b.get("year", {}).get("value", ""))
            sitelinks = (
                _safe_int(b.get("sitelinks", {}).get("value", "0"))
                or 0
            )
            rows.append((work_uri, year, sitelinks))

        offset += query_limit
        _progress(
            f"Wikidata IDs: {len(rows)}/{target_pool} "
            f"(offset={offset})"
        )
        time.sleep(max(0.0, internal_sleep_s))

    return rows


def _fetch_candidate_metadata(
    candidates: list[tuple[str, Optional[int], int]],
    *,
    include_en_labels: bool,
    internal_sleep_s: float,
) -> list[CandidateRow]:
    if not candidates:
        return []

    base: dict[str, tuple[Optional[int], int]] = {
        uri: (year, sitelinks)
        for uri, year, sitelinks in candidates
    }

    work_qids = [_qid_from_entity_uri(uri) for uri in base.keys()]
    work_qids = [q for q in work_qids if q]
    out: list[CandidateRow] = []

    languages = "es|en"

    batch_size = 50
    author_ids: set[str] = set()
    work_to_author_id: dict[str, str] = {}

    total_batches = (len(work_qids) + batch_size - 1) // batch_size
    for i in range(0, len(work_qids), batch_size):
        batch_idx = (i // batch_size) + 1
        _progress(
            f"Wikidata API meta: lote {batch_idx}/{total_batches} "
            f"({min(i + batch_size, len(work_qids))}/{len(work_qids)})"
        )
        ids = "|".join(work_qids[i:i + batch_size])
        params = {
            "action": "wbgetentities",
            "format": "json",
            "formatversion": "2",
            "ids": ids,
            "props": "labels|aliases|sitelinks|claims",
            "languages": languages,
            "sitefilter": (
                "eswiki|eswikisource|eswikibooks|eswikiquote|enwiki"
            ),
        }
        try:
            raw = _run_wikidata_api(
                params,
                retries=2,
                sleep_s=2.0,
                timeout_s=60,
                tag=f"Wikidata API meta lote {batch_idx}/{total_batches}",
            )
        except RuntimeError as exc:
            _progress(
                f"Wikidata API meta: lote {batch_idx} falló (se omite): {exc}"
            )
            time.sleep(2.0)
            continue

        entities_obj = raw.get("entities", [])
        entities: list[dict[str, Any]] = []
        if isinstance(entities_obj, list):
            entities = [e for e in entities_obj if isinstance(e, dict)]
        elif isinstance(entities_obj, dict):
            entities = [
                e for e in entities_obj.values() if isinstance(e, dict)
            ]
        else:
            continue

        for ent in entities:
            qid = str(ent.get("id", "")).strip()
            if not qid:
                continue
            work_uri = f"http://www.wikidata.org/entity/{qid}"
            year, sitelinks = base.get(work_uri, (None, 0))

            labels = ent.get("labels", {})
            title_es = ""
            title_en = ""
            if isinstance(labels, dict):
                es_obj = labels.get("es")
                if isinstance(es_obj, dict):
                    title_es = str(es_obj.get("value", "")).strip()
                en_obj = labels.get("en")
                if isinstance(en_obj, dict):
                    title_en = str(en_obj.get("value", "")).strip()

            es_article = ""
            en_article = ""
            sitelinks_obj = ent.get("sitelinks", {})
            if isinstance(sitelinks_obj, dict):
                for site in (
                    "eswiki",
                    "eswikisource",
                    "eswikibooks",
                    "eswikiquote",
                ):
                    link = sitelinks_obj.get(site)
                    if not isinstance(link, dict):
                        continue
                    es_title = str(link.get("title", "")).strip()
                    es_url = str(link.get("url", "")).strip()
                    if es_url:
                        es_article = es_url
                        break
                    if es_title:
                        es_article = _url_from_wikimedia_sitelink(
                            site,
                            es_title,
                        )
                        if es_article:
                            break

                enwiki = sitelinks_obj.get("enwiki")
                if isinstance(enwiki, dict):
                    en_title = str(enwiki.get("title", "")).strip()
                    en_url = str(enwiki.get("url", "")).strip()
                    if en_url:
                        en_article = en_url
                    elif en_title:
                        en_article = _url_from_wikimedia_sitelink(
                            "enwiki",
                            en_title,
                        )

            author_id = ""
            claims = ent.get("claims", {})
            p1476_es = _extract_p1476_title_es(claims)
            if not title_es and p1476_es:
                title_es = p1476_es
            if not title_es:
                title_es = _extract_monolingual_text_es(claims, "P2561")
            if not title_es:
                title_es = _extract_monolingual_text_es(claims, "P1448")
            if not title_es:
                title_es = _extract_monolingual_text_es(claims, "P1813")
            if not title_es:
                title_es = _extract_alias_title_es(ent)

            openlibrary_id = _extract_p648_openlibrary_id(claims)
            bne_id = _extract_p950_bne_id(claims)
            if isinstance(claims, dict):
                p50 = claims.get("P50")
                if isinstance(p50, list) and p50:
                    mainsnak = p50[0].get("mainsnak", {})
                    datavalue = mainsnak.get("datavalue", {})
                    value = datavalue.get("value", {})
                    if isinstance(value, dict):
                        author_id = str(value.get("id", "")).strip()

            if author_id:
                author_ids.add(author_id)
                work_to_author_id[qid] = author_id

            out.append(
                CandidateRow(
                    work_uri=work_uri,
                    title_es=title_es,
                    title_en=title_en,
                    es_article=es_article,
                    en_article=en_article,
                    author_es="",
                    author_en="",
                    openlibrary_id=openlibrary_id,
                    bne_id=bne_id,
                    year=year,
                    sitelinks=sitelinks,
                )
            )

        time.sleep(max(0.0, internal_sleep_s))

    if not out:
        return []

    author_label_es: dict[str, str] = {}
    author_label_en: dict[str, str] = {}

    author_qids = sorted(author_ids)
    if author_qids:
        author_batch_size = 50
        total_author_batches = (
            (len(author_qids) + author_batch_size - 1) // author_batch_size
        )
        for i in range(0, len(author_qids), author_batch_size):
            batch_idx = (i // author_batch_size) + 1
            _progress(
                "Wikidata API autores: lote "
                f"{batch_idx}/{total_author_batches}"
            )
            ids = "|".join(author_qids[i:i + author_batch_size])
            params = {
                "action": "wbgetentities",
                "format": "json",
                "formatversion": "2",
                "ids": ids,
                "props": "labels",
                "languages": languages,
            }
            try:
                raw = _run_wikidata_api(
                    params,
                    retries=2,
                    sleep_s=2.0,
                    timeout_s=60,
                    tag=(
                        "Wikidata API autores "
                        f"lote {batch_idx}/{total_author_batches}"
                    ),
                )
            except RuntimeError as exc:
                _progress(
                    "Wikidata API autores: lote falló (se omite): "
                    f"{exc}"
                )
                time.sleep(2.0)
                continue

            entities_obj = raw.get("entities", [])
            entities: list[dict[str, Any]] = []
            if isinstance(entities_obj, list):
                entities = [e for e in entities_obj if isinstance(e, dict)]
            elif isinstance(entities_obj, dict):
                entities = [
                    e for e in entities_obj.values() if isinstance(e, dict)
                ]
            else:
                continue

            for ent in entities:
                qid = str(ent.get("id", "")).strip()
                labels = ent.get("labels", {})
                if not isinstance(labels, dict):
                    continue
                es_obj = labels.get("es")
                if isinstance(es_obj, dict):
                    author_label_es[qid] = str(es_obj.get("value", "")).strip()
                en_obj = labels.get("en")
                if isinstance(en_obj, dict):
                    author_label_en[qid] = str(en_obj.get("value", "")).strip()
            time.sleep(max(0.0, internal_sleep_s))

    out2: list[CandidateRow] = []
    for row in out:
        qid = _qid_from_entity_uri(row.work_uri)
        author_id = work_to_author_id.get(qid, "")
        out2.append(
            CandidateRow(
                work_uri=row.work_uri,
                title_es=row.title_es,
                title_en=row.title_en,
                es_article=row.es_article,
                en_article=row.en_article,
                author_es=author_label_es.get(author_id, ""),
                author_en=author_label_en.get(author_id, ""),
                openlibrary_id=row.openlibrary_id,
                bne_id=row.bne_id,
                year=row.year,
                sitelinks=row.sitelinks,
            )
        )

    return out2


def _load_cache(path: Path) -> dict[str, Optional[str]]:
    if not path.exists():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(data, dict):
            return {
                str(k): (v if v is None else str(v))
                for k, v in data.items()
            }
    except Exception:  # noqa: BLE001
        return {}
    return {}


def _save_cache(path: Path, cache: dict[str, Optional[str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(cache, ensure_ascii=False, indent=2, sort_keys=True),
        encoding="utf-8",
    )


def _openlibrary_search_spanish_title(
    *,
    title_hint: str,
    author_hint: str,
    sleep_s: float,
    cache: dict[str, Optional[str]],
) -> Optional[str]:
    key = _normalize_key(f"v2||{title_hint}||{author_hint}")
    if key in cache:
        return cache[key]

    def _looks_spanish(text: str) -> bool:
        lowered = text.lower()
        if any(ch in lowered for ch in ("á", "é", "í", "ó", "ú", "ñ")):
            return True
        if lowered.startswith(("el ", "la ", "los ", "las ", "un ", "una ")):
            return True
        return False

    def _looks_non_spanish(text: str) -> bool:
        lowered = text.lower()
        if _looks_spanish(lowered):
            return False
        if any(w in lowered for w in (" the ", " and ", " of ", " a ")):
            return True
        return False

    def _extract_first_year(doc: dict[str, Any]) -> Optional[int]:
        first = doc.get("first_publish_year")
        if isinstance(first, int):
            return first
        if isinstance(first, str):
            return _safe_int(first)
        years = doc.get("publish_year")
        if isinstance(years, list) and years:
            parsed = [
                _safe_int(str(y))
                for y in years
                if _safe_int(str(y)) is not None
            ]
            if parsed:
                return min(parsed)
        return None

    def _query(
        params: dict[str, str],
        *,
        require_spa: bool,
    ) -> Optional[str]:
        url = f"{OPENLIBRARY_SEARCH_ENDPOINT}?{urllib.parse.urlencode(params)}"
        time.sleep(max(0.0, sleep_s))
        try:
            raw = _http_get(url)
            payload = json.loads(raw.decode("utf-8"))
        except Exception:  # noqa: BLE001
            return None

        docs = payload.get("docs", [])
        if not isinstance(docs, list):
            return None

        best_local: Optional[str] = None
        for doc in docs:
            if not isinstance(doc, dict):
                continue
            title = str(doc.get("title", "")).strip()
            if not title:
                continue

            languages = doc.get("language", [])
            if isinstance(languages, list) and "spa" in languages:
                return title

            if require_spa:
                continue

            year = _extract_first_year(doc)
            if year is not None and year > 1900:
                continue

            if best_local is None and _looks_spanish(title):
                best_local = title

        return best_local

    params_spa_author = {
        "title": title_hint,
        "author": author_hint,
        "language": "spa",
        "limit": "10",
    }
    params_spa_title = {
        "title": title_hint,
        "language": "spa",
        "limit": "10",
    }
    params_any_author = {
        "title": title_hint,
        "author": author_hint,
        "limit": "10",
    }
    params_any_title = {
        "title": title_hint,
        "limit": "10",
    }

    attempts: list[dict[str, str]] = [
        params_spa_author,
        params_spa_title,
        params_any_author,
        params_any_title,
    ]

    best: Optional[str] = None
    for params in attempts:
        require_spa = params.get("language") == "spa"
        best = _query(params, require_spa=require_spa)
        if best:
            break

    cache[key] = best
    return best


def _openlibrary_editions_spanish_title(
    *,
    openlibrary_id: str,
    sleep_s: float,
    cache: dict[str, Optional[str]],
) -> Optional[str]:
    olid = openlibrary_id.strip()
    if not olid:
        return None

    cache_key = _normalize_key(f"ed_v1||{olid}")
    if cache_key in cache:
        return cache[cache_key]

    def _is_spanish_edition(ed: dict[str, Any]) -> bool:
        langs = ed.get("languages", [])
        if not isinstance(langs, list):
            return False
        for lang in langs:
            if not isinstance(lang, dict):
                continue
            key = str(lang.get("key", "")).strip()
            if key == "/languages/spa":
                return True
        return False

    best: Optional[str] = None
    for offset in (0, 50):
        url = (
            f"{OPENLIBRARY_WORKS_ENDPOINT}/{urllib.parse.quote(olid)}/"
            f"editions.json?limit=50&offset={offset}"
        )
        time.sleep(max(0.0, sleep_s))
        try:
            raw = _http_get(url)
            payload = json.loads(raw.decode("utf-8"))
        except Exception:  # noqa: BLE001
            continue

        entries = payload.get("entries", [])
        if not isinstance(entries, list):
            continue
        for ed in entries:
            if not isinstance(ed, dict):
                continue
            if not _is_spanish_edition(ed):
                continue
            title = str(ed.get("title", "")).strip()
            if title:
                best = title
                break
        if best:
            break

    cache[cache_key] = best
    return best


def _wikipedia_es_title_from_enwiki(
    *,
    en_article_url: str,
    sleep_s: float,
    cache: dict[str, Optional[str]],
) -> Optional[str]:
    en_title = _title_from_eswiki_url(en_article_url)
    en_title = _normalize_display_text(en_title)
    if not en_title:
        return None

    cache_key = _normalize_key(f"wikill_v1||{en_title}")
    if cache_key in cache:
        return cache[cache_key]

    params = {
        "action": "query",
        "format": "json",
        "formatversion": "2",
        "titles": en_title,
        "prop": "langlinks",
        "lllang": "es",
        "lllimit": "1",
    }
    url = f"{WIKIPEDIA_EN_API_ENDPOINT}?{urllib.parse.urlencode(params)}"
    time.sleep(max(0.0, sleep_s))
    try:
        raw = _http_get(url, timeout_s=60)
        payload = json.loads(raw.decode("utf-8"))
    except Exception as exc:  # noqa: BLE001
        if not _is_transient_request_error(exc):
            cache[cache_key] = None
        return None

    pages = payload.get("query", {}).get("pages", [])
    if not isinstance(pages, list) or not pages:
        cache[cache_key] = None
        return None
    page0 = pages[0]
    if not isinstance(page0, dict):
        cache[cache_key] = None
        return None
    langlinks = page0.get("langlinks", [])
    if not isinstance(langlinks, list) or not langlinks:
        cache[cache_key] = None
        return None
    ll0 = langlinks[0]
    if not isinstance(ll0, dict):
        cache[cache_key] = None
        return None
    title_es = str(ll0.get("title", "")).strip()
    title_es = _normalize_display_text(title_es)
    best: Optional[str] = title_es or None
    cache[cache_key] = best
    return best


def _wikipedia_prefetch_es_titles_from_enwiki(
    *,
    en_titles: list[str],
    sleep_s: float,
    cache: dict[str, Optional[str]],
) -> None:
    cleaned: list[str] = []
    seen: set[str] = set()
    for t in en_titles:
        norm = _normalize_display_text(t)
        if not norm:
            continue
        key = _normalize_key(f"wikill_v1||{norm}")
        if key in cache:
            continue
        if norm in seen:
            continue
        seen.add(norm)
        cleaned.append(norm)

    if not cleaned:
        return

    batch_size = 50
    for i in range(0, len(cleaned), batch_size):
        batch = cleaned[i:i + batch_size]
        params = {
            "action": "query",
            "format": "json",
            "formatversion": "2",
            "titles": "|".join(batch),
            "prop": "langlinks",
            "lllang": "es",
            "lllimit": "1",
        }
        url = (
            f"{WIKIPEDIA_EN_API_ENDPOINT}?{urllib.parse.urlencode(params)}"
        )
        time.sleep(max(0.0, sleep_s))
        try:
            raw = _http_get(url, timeout_s=60)
            payload = json.loads(raw.decode("utf-8"))
        except Exception as exc:  # noqa: BLE001
            if not _is_transient_request_error(exc):
                for t in batch:
                    cache_key = _normalize_key(f"wikill_v1||{t}")
                    cache.setdefault(cache_key, None)
            continue

        pages = payload.get("query", {}).get("pages", [])
        if not isinstance(pages, list):
            continue

        for page in pages:
            if not isinstance(page, dict):
                continue
            en_title = str(page.get("title", "")).strip()
            en_title = _normalize_display_text(en_title)
            if not en_title:
                continue
            cache_key = _normalize_key(f"wikill_v1||{en_title}")
            if cache_key in cache:
                continue
            langlinks = page.get("langlinks", [])
            if not isinstance(langlinks, list) or not langlinks:
                cache[cache_key] = None
                continue
            ll0 = langlinks[0]
            if not isinstance(ll0, dict):
                cache[cache_key] = None
                continue
            title_es = str(ll0.get("title", "")).strip()
            title_es = _normalize_display_text(title_es)
            cache[cache_key] = title_es or None


def _bne_extract_title_from_jsonld(payload: Any, bne_id: str) -> str:
    if not isinstance(payload, dict):
        return ""
    graph = payload.get("@graph", [])
    if not isinstance(graph, list):
        return ""

    candidates: list[dict[str, Any]] = [
        n for n in graph if isinstance(n, dict)
    ]

    target_uri = f"{BNE_RESOURCE_ENDPOINT}/{bne_id}"
    preferred = [
        n for n in candidates if str(n.get("@id", "")).strip() == target_uri
    ]
    ordered = preferred + [n for n in candidates if n not in preferred]

    def _extract_label(node: dict[str, Any]) -> str:
        label = node.get("label")
        if isinstance(label, str):
            return label.strip()
        if isinstance(label, dict):
            val = str(label.get("@value", "")).strip()
            if val:
                return val
        pref = node.get("prefLabel")
        if isinstance(pref, str):
            return pref.strip()
        if isinstance(pref, dict):
            val = str(pref.get("@value", "")).strip()
            if val:
                return val
        p3002 = node.get("P3002")
        if isinstance(p3002, str):
            return p3002.strip()
        return ""

    for node in ordered:
        title = _extract_label(node)
        title = _normalize_display_text(title)
        if title:
            return title
    return ""


def _bne_spanish_title(
    *,
    bne_id: str,
    sleep_s: float,
    cache: dict[str, Optional[str]],
) -> Optional[str]:
    bne_id = bne_id.strip()
    if not bne_id:
        return None

    cache_key = _normalize_key(f"bne_v1||{bne_id}")
    if cache_key in cache:
        return cache[cache_key]

    url = (
        f"{BNE_RESOURCE_ENDPOINT}/{urllib.parse.quote(bne_id)}.jsonld"
    )
    time.sleep(max(0.0, sleep_s))
    try:
        raw = _http_get(url)
        payload = json.loads(raw.decode("utf-8"))
    except Exception as exc:  # noqa: BLE001
        if not _is_transient_request_error(exc):
            cache[cache_key] = None
        return None

    title = _bne_extract_title_from_jsonld(payload, bne_id)
    best: Optional[str] = title or None
    cache[cache_key] = best
    return best


def build_lines(
    rows: Iterable[CandidateRow],
    *,
    limit: int,
    require_eswiki_title: bool,
    prefer_eswiki_title: bool,
    allow_english_fallback: bool,
    use_wiki_langlinks: bool,
    wiki_langlinks_sleep_s: float,
    max_wiki_langlinks: int,
    use_bne: bool,
    bne_sleep_s: float,
    max_bne_lookups: int,
    use_openlibrary: bool,
    openlibrary_sleep_s: float,
    max_openlibrary_lookups: int,
    cache: dict[str, Optional[str]],
) -> list[str]:
    seen: set[str] = set()
    output: list[str] = []
    openlibrary_lookups = 0
    bne_lookups = 0
    wiki_langlinks_lookups = 0

    def _looks_spanish(text: str) -> bool:
        lowered = text.lower()
        if any(ch in lowered for ch in ("á", "é", "í", "ó", "ú", "ñ")):
            return True
        if lowered.startswith(("el ", "la ", "los ", "las ", "un ", "una ")):
            return True
        return False

    if use_wiki_langlinks:
        prefetch: list[str] = []
        for row in rows:
            if len(prefetch) >= max_wiki_langlinks:
                break
            title_es_norm = _normalize_display_text(row.title_es)
            title_en_norm = _normalize_display_text(row.title_en)
            english_suspect = bool(
                title_es_norm
                and title_en_norm
                and title_es_norm == title_en_norm
                and not _looks_spanish(title_es_norm)
            )

            if row.es_article:
                continue
            if title_es_norm and not english_suspect:
                continue
            if not row.en_article:
                continue
            en_title = _title_from_eswiki_url(row.en_article)
            en_title = _normalize_display_text(en_title)
            if en_title:
                prefetch.append(en_title)

        if prefetch:
            _progress(
                f"Wikipedia langlinks: precargando {len(prefetch)} títulos..."
            )
            _wikipedia_prefetch_es_titles_from_enwiki(
                en_titles=prefetch,
                sleep_s=wiki_langlinks_sleep_s,
                cache=cache,
            )

    for row in rows:
        author = row.author_es or row.author_en or "Desconocido"
        if (
            author.startswith("http://")
            or author.startswith("https://")
            or (author.startswith("Q") and author[1:].isdigit())
        ):
            author = "Desconocido"
        author = _normalize_display_text(author)
        if not author:
            author = "Desconocido"

        title: str = ""
        if require_eswiki_title:
            title = _title_from_eswiki_url(row.es_article)
            if not title:
                continue
        else:
            if prefer_eswiki_title:
                title = _title_from_eswiki_url(row.es_article) or row.title_es
            else:
                title = row.title_es

            if not title:
                title = _title_from_eswiki_url(row.es_article)

            title_norm = _normalize_display_text(title)
            if (
                title_norm
                and row.title_en
                and title_norm == _normalize_display_text(row.title_en)
            ):
                lowered = title_norm.lower()
                if not any(
                    ch in lowered
                    for ch in ("á", "é", "í", "ó", "ú", "ñ")
                ):
                    if any(w in lowered for w in (" the ", " and ", " of ")):
                        title = ""

            if (
                not title
                and use_wiki_langlinks
                and wiki_langlinks_lookups < max_wiki_langlinks
                and row.en_article
            ):
                title = (
                    _wikipedia_es_title_from_enwiki(
                        en_article_url=row.en_article,
                        sleep_s=wiki_langlinks_sleep_s,
                        cache=cache,
                    )
                    or ""
                )
                wiki_langlinks_lookups += 1
                if wiki_langlinks_lookups % 100 == 0:
                    _progress(
                        "Wikipedia langlinks: "
                        f"{wiki_langlinks_lookups}/{max_wiki_langlinks}"
                    )

            if (
                not title
                and use_bne
                and bne_lookups < max_bne_lookups
                and row.bne_id
            ):
                title = (
                    _bne_spanish_title(
                        bne_id=row.bne_id,
                        sleep_s=bne_sleep_s,
                        cache=cache,
                    )
                    or ""
                )
                bne_lookups += 1
                if bne_lookups % 50 == 0:
                    _progress(
                        f"BNE: {bne_lookups}/{max_bne_lookups}"
                    )

            if (
                not title
                and use_openlibrary
                and openlibrary_lookups < max_openlibrary_lookups
            ):
                if row.openlibrary_id:
                    title = (
                        _openlibrary_editions_spanish_title(
                            openlibrary_id=row.openlibrary_id,
                            sleep_s=openlibrary_sleep_s,
                            cache=cache,
                        )
                        or ""
                    )

            if (
                not title
                and use_openlibrary
                and openlibrary_lookups < max_openlibrary_lookups
            ):
                title_hint = (
                    row.title_en
                    or row.title_es
                    or _title_from_eswiki_url(row.es_article)
                )
                title_hint = _normalize_display_text(title_hint)
                title = (
                    _openlibrary_search_spanish_title(
                        title_hint=title_hint,
                        author_hint=author if author != "Desconocido" else "",
                        sleep_s=openlibrary_sleep_s,
                        cache=cache,
                    )
                    or ""
                )
                openlibrary_lookups += 1
                if openlibrary_lookups % 25 == 0:
                    _progress(
                        "Open Library: "
                        f"{openlibrary_lookups}/{max_openlibrary_lookups}"
                    )

            if not title and allow_english_fallback:
                title = row.title_en

        title = _normalize_display_text(title)
        if not title:
            continue

        if title.startswith("http://") or title.startswith("https://"):
            continue

        line = f"{author} - {title}".strip()
        key = _normalize_key(line)
        if not key or key in seen:
            continue

        seen.add(key)
        output.append(line)
        if len(output) % 100 == 0:
            _progress(f"Salida: {len(output)}/{limit} líneas")
        if len(output) >= limit:
            break

    return output


def write_lines(lines: list[str], out_path: Path) -> None:
    out_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _goodreads_extract_first_nonempty(
    row: dict[str, str],
    keys: list[str],
) -> str:
    for k in keys:
        val = row.get(k)
        if not val:
            continue
        val = str(val).strip()
        if val:
            return val
    return ""


def _goodreads_extract_year(row: dict[str, str]) -> Optional[int]:
    year_raw = _goodreads_extract_first_nonempty(
        row,
        [
            "original_publication_year",
            "publication_year",
            "first_publish_year",
            "year",
        ],
    )
    if not year_raw:
        return None
    year_raw = year_raw.strip()
    if not year_raw:
        return None
    try:
        return int(float(year_raw))
    except Exception:
        return None


def _goodreads_iter_spanish_lines(path: Path) -> Iterable[str]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8", newline="") as fin:
        reader = csv.DictReader(fin)
        for row_obj in reader:
            if not isinstance(row_obj, dict):
                continue
            row = {str(k): str(v) for k, v in row_obj.items() if k is not None}

            title = _goodreads_extract_first_nonempty(
                row,
                [
                    "title",
                    "book_title",
                    "original_title",
                ],
            )
            author = _goodreads_extract_first_nonempty(
                row,
                [
                    "authors",
                    "author",
                    "book_author",
                ],
            )

            year = _goodreads_extract_year(row)
            if year is None or year > 1900:
                continue

            title = _normalize_display_text(title)
            author = _normalize_display_text(author)
            if not title or not author:
                continue

            if title.startswith("http://") or title.startswith("https://"):
                continue
            if author.startswith("http://") or author.startswith("https://"):
                continue

            yield f"{author} - {title}".strip()


def _fill_with_goodreads(
    *,
    lines: list[str],
    limit: int,
    goodreads_es_csv: Path,
) -> list[str]:
    if limit <= 0:
        return lines
    if len(lines) >= limit:
        return lines

    seen: set[str] = set()
    for existing_line in lines:
        key = _normalize_key(existing_line)
        if key:
            seen.add(key)

    added = 0
    for line in _goodreads_iter_spanish_lines(goodreads_es_csv):
        key = _normalize_key(line)
        if not key or key in seen:
            continue
        seen.add(key)
        lines.append(line)
        added += 1
        if len(lines) >= limit:
            break

    if added:
        _progress(f"Goodreads: añadidas {added} líneas")
    return lines


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Generate a TXT list of important novels (<=1900) with Spanish "
            "titles using Wikidata + optional Open Library enrichment."
        )
    )
    parser.add_argument("--limit", type=int, default=1000)

    parser.add_argument(
        "--candidate-pool",
        type=int,
        default=0,
        help=(
            "How many Wikidata candidate works to fetch before enrichment. "
            "0 means auto. Larger values increase coverage but slow down."
        ),
    )

    parser.add_argument(
        "--goodreads-es-csv",
        type=str,
        default="",
        help=(
            "Optional path to a Goodreads-like CSV already filtered to "
            "Spanish. Used only to fill remaining slots if Wikidata sources "
            "produce < limit."
        ),
    )

    eswiki_group = parser.add_mutually_exclusive_group()
    eswiki_group.add_argument("--require-eswiki-title", action="store_true")
    eswiki_group.add_argument("--prefer-eswiki-title", action="store_true")

    parser.add_argument(
        "--no-openlibrary",
        action="store_true",
        default=False,
        help="Disable Open Library enrichment.",
    )

    parser.add_argument(
        "--no-bne",
        action="store_true",
        default=False,
        help="Disable datos.bne.es enrichment.",
    )

    parser.add_argument(
        "--no-wiki-langlinks",
        action="store_true",
        default=False,
        help="Disable enwiki->es interlanguage link enrichment.",
    )

    parser.add_argument(
        "--wiki-langlinks-sleep-s",
        type=float,
        default=0.25,
    )

    parser.add_argument(
        "--max-wiki-langlinks",
        type=int,
        default=2000,
    )

    parser.add_argument(
        "--bne-sleep-s",
        type=float,
        default=0.35,
    )

    parser.add_argument(
        "--max-bne-lookups",
        type=int,
        default=2000,
    )

    parser.add_argument(
        "--openlibrary-sleep-s",
        type=float,
        default=0.25,
    )

    parser.add_argument(
        "--internal-sleep-s",
        type=float,
        default=0.0,
        help=(
            "Internal throttling between Wikidata batches. Increase if you "
            "get 429/503 from Wikidata; set to 0 for max speed."
        ),
    )

    parser.add_argument(
        "--max-openlibrary-lookups",
        type=int,
        default=2000,
    )

    parser.add_argument(
        "--min-sitelinks",
        type=int,
        default=0,
    )

    parser.add_argument(
        "--include-en-labels",
        action="store_true",
        default=False,
        help=(
            "Fetch English labels from Wikidata too (slower; may trigger more "
            "504)."
        ),
    )

    parser.add_argument(
        "--no-progress",
        action="store_true",
        default=False,
    )

    parser.add_argument("--allow-english-fallback", action="store_true")

    parser.add_argument(
        "--cache",
        type=str,
        default=str(Path(__file__).with_suffix(".openlibrary_cache.json")),
    )

    parser.add_argument(
        "--out",
        type=str,
        default="novelas_1000_pre1900_multisource.txt",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    global PROGRESS_ENABLED
    PROGRESS_ENABLED = not bool(args.no_progress)

    if args.limit <= 0:
        raise SystemExit("--limit must be > 0")

    use_openlibrary = not bool(args.no_openlibrary)
    use_bne = not bool(args.no_bne)
    use_wiki_langlinks = not bool(args.no_wiki_langlinks)

    cache_path = Path(args.cache)
    cache = _load_cache(cache_path)

    _progress("Iniciando generación multisource...")
    _progress(f"limit={args.limit}")
    _progress(f"wiki_langlinks={'ON' if use_wiki_langlinks else 'OFF'}")
    _progress(f"bne={'ON' if use_bne else 'OFF'}")
    _progress(f"openlibrary={'ON' if use_openlibrary else 'OFF'}")
    if use_wiki_langlinks:
        _progress(f"max_wiki_langlinks={int(args.max_wiki_langlinks)}")
    if use_bne:
        _progress(f"max_bne_lookups={int(args.max_bne_lookups)}")
    if use_openlibrary:
        _progress(
            f"max_openlibrary_lookups={int(args.max_openlibrary_lookups)}"
        )
    _progress(f"min_sitelinks={int(args.min_sitelinks)}")
    _progress(f"include_en_labels={bool(args.include_en_labels)}")
    _progress(f"internal_sleep_s={float(args.internal_sleep_s):.2f}")
    _progress(f"candidate_pool={int(args.candidate_pool)}")

    rows = fetch_candidates(
        limit=int(args.limit),
        min_sitelinks=int(args.min_sitelinks),
        include_en_labels=bool(args.include_en_labels),
        internal_sleep_s=float(args.internal_sleep_s),
        candidate_pool=int(args.candidate_pool),
    )

    lines = build_lines(
        rows,
        limit=args.limit,
        require_eswiki_title=bool(args.require_eswiki_title),
        prefer_eswiki_title=bool(args.prefer_eswiki_title),
        allow_english_fallback=bool(args.allow_english_fallback),
        use_wiki_langlinks=use_wiki_langlinks,
        wiki_langlinks_sleep_s=float(args.wiki_langlinks_sleep_s),
        max_wiki_langlinks=int(args.max_wiki_langlinks),
        use_bne=use_bne,
        bne_sleep_s=float(args.bne_sleep_s),
        max_bne_lookups=int(args.max_bne_lookups),
        use_openlibrary=use_openlibrary,
        openlibrary_sleep_s=float(args.openlibrary_sleep_s),
        max_openlibrary_lookups=int(args.max_openlibrary_lookups),
        cache=cache,
    )

    goodreads_path = Path(str(args.goodreads_es_csv)).expanduser()
    if str(args.goodreads_es_csv).strip():
        lines = _fill_with_goodreads(
            lines=lines,
            limit=int(args.limit),
            goodreads_es_csv=goodreads_path,
        )

    _save_cache(cache_path, cache)

    if not lines:
        raise SystemExit("No results produced.")

    out_path = Path(args.out)
    write_lines(lines, out_path)
    print(f"Wrote {len(lines)} lines to: {out_path}")


if __name__ == "__main__":
    main()
