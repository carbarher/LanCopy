"""Utilities for extracting unique Gutenberg authors.

This script reads the Project Gutenberg metadata CSV and produces a
UTF-8 text file with one author per line. The CSV is expected to live at
`c:/p2p/gutenberg_metadata.csv`, and the output will be written to
`SlskDown/Data/gutenberg_authors.txt` relative to the repository root.

Run with: `python scripts/extract_gutenberg_authors.py`
"""

from __future__ import annotations

import csv
import json
import re
import hashlib
import unicodedata
from pathlib import Path
from typing import Iterable, Set

REPO_ROOT = Path(__file__).resolve().parents[1]
GUTENBERG_CSV = REPO_ROOT / "gutenberg_metadata.csv"
OUTPUT_FILE = REPO_ROOT / "Data" / "gutenberg_authors.txt"
CACHE_FILE = REPO_ROOT / "Data" / "gutenberg_authors_cache.json"

DEFAULT_CANONICAL_AUTHORS: list[str] = [
    "William Shakespeare",
    "León Tolstói",
    "Dante Alighieri",
    "Miguel de Cervantes",
    "Gabriel García Márquez",
    "Jorge Luis Borges",
    "Federico García Lorca",
    "Benito Pérez Galdós",
    "Lope de Vega",
    "Pedro Calderón de la Barca",
    "Pablo Neruda",
    "Mario Vargas Llosa",
    "Julio Cortázar",
    "Isabel Allende",
    "Fiódor Dostoyevski",
    "Charles Dickens",
    "Homero",
    "Víctor Hugo",
    "James Joyce",
    "Franz Kafka",
    "Johann Wolfgang von Goethe",
    "Jane Austen",
    "Honoré de Balzac",
    "Gustave Flaubert",
    "Edgar Allan Poe",
    "Herman Melville",
    "Antón Chéjov",
    "Marcel Proust",
    "Virginia Woolf",
    "Mark Twain",
]


def load_canonical_authors() -> list[str]:
    canonical_file = REPO_ROOT / "Data" / "canonical_authors_priority.txt"
    if canonical_file.exists():
        with canonical_file.open(encoding="utf-8") as handle:
            lines = [line.strip() for line in handle]
        canonical = [name for name in lines if name]
        if canonical:
            return list(dict.fromkeys(canonical))

    return DEFAULT_CANONICAL_AUTHORS


CANONICAL_AUTHORS: list[str] = load_canonical_authors()


def compute_hash_from_path(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(8192), b""):
            digest.update(chunk)
    return digest.hexdigest()


def compute_hash_from_list(values: Iterable[str]) -> str:
    digest = hashlib.sha256()
    for value in values:
        digest.update(value.encode("utf-8"))
        digest.update(b"\0")
    return digest.hexdigest()


def normalize_author(raw_author: str) -> str:
    """Return a trimmed author string or an empty string if invalid.

    This removes obvious noise such as parenthetical notes and collapses
    whitespace, but still returns the original display form (not lowercased).
    """
    cleaned = raw_author.strip().strip('"')
    cleaned = re.sub(r"\s*\([^)]*\)", "", cleaned)
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    return cleaned if cleaned else ""


def looks_like_person(author: str) -> bool:
    """Heuristic to discard entries that are not real people.

    We drop entries that are clearly:
    - pure years or year ranges ("1500-1700", "1485-1534")
    - mostly digits and punctuation
    - very short symbolic tokens ("c", "_Treatment", etc.)
    - subject-like strings with "--" (Gutenberg subjects)
    - descriptive subjects with ';'
    - long descriptive phrases without a comma
    """

    if not author:
        return False

    # Too short or mostly punctuation/digits
    if len(author) < 3:
        return False

    # Discard obvious non-name prefixes
    if author[0] in {"_", "'", "(", ")", "[", "]"}:
        return False

    # Discard Gutenberg subject-style entries and descriptive phrases
    if "--" in author or ";" in author:
        return False

    # If it contains digits anywhere, it's very likely a date / range / code
    if any(ch.isdigit() for ch in author):
        return False

    tokens = author.split()

    # Single-token codes (e.g. "PR", "ABC")
    if len(tokens) == 1:
        token = tokens[0]
        if not any(ch.islower() for ch in token):
            return False

    # If there is a comma, we assume it's a "Surname, Name" style and accept.
    if "," in author:
        return True

    # Without comma: require at least two capitalized tokens
    # that look like names
    capitalized_tokens = [t for t in tokens if t and t[0].isupper()]
    if len(capitalized_tokens) < 2:
        return False

    return True


def canonical_author_key(author: str) -> str:
    """Produce a canonical key so variant spellings map to the same author."""
    normalized = strip_accents(author.casefold())
    normalized = normalized.replace(".", " ")
    normalized = re.sub(r"[^\w\s]", "", normalized)
    tokens = normalized.split()

    canonical_parts: list[str] = []
    initials_buffer: list[str] = []

    for token in tokens:
        if token.isalpha() and len(token) == 1:
            initials_buffer.append(token)
            continue

        if (
            token.isalpha()
            and len(token) <= 3
            and len(set(token)) == 1
        ):
            initials_buffer.extend(token)
            continue

        if initials_buffer:
            canonical_parts.append("".join(initials_buffer))
            initials_buffer = []

        canonical_parts.append(token)

    if initials_buffer:
        canonical_parts.append("".join(initials_buffer))

    return "".join(canonical_parts)


def strip_accents(text: str) -> str:
    """Remove diacritical marks while preserving base characters."""
    normalized = unicodedata.normalize("NFKD", text)
    return "".join(
        char for char in normalized if not unicodedata.combining(char)
    )


def extract_unique_authors(rows: Iterable[dict[str, str]]) -> list[str]:
    """Collect unique author names from the iterable of CSV rows."""
    seen_keys: Set[str] = set()
    unique_authors: list[str] = []

    canonical_priority_map = {
        canonical_author_key(name): index
        for index, name in enumerate(CANONICAL_AUTHORS)
    }
    default_priority = len(CANONICAL_AUTHORS)

    for row in rows:
        raw_authors = row.get("Authors", "")
        if not raw_authors:
            continue

        for candidate in raw_authors.split(";"):
            author = normalize_author(candidate)
            if not author:
                continue

            if not looks_like_person(author):
                continue

            key = canonical_author_key(author)
            if key in seen_keys:
                continue

            seen_keys.add(key)
            unique_authors.append(author)

    def sort_key(author: str) -> tuple[int, str]:
        canonical_key = canonical_author_key(author)
        priority = canonical_priority_map.get(canonical_key, default_priority)
        return priority, strip_accents(author.casefold())

    unique_authors.sort(key=sort_key)
    return unique_authors


def read_gutenberg_rows(path: Path) -> Iterable[dict[str, str]]:
    """Yield rows from the Gutenberg metadata CSV file."""
    with path.open(newline="", encoding="utf-8") as csv_file:
        reader = csv.DictReader(csv_file)
        for row in reader:
            yield row


def write_authors(authors: Iterable[str], destination: Path) -> None:
    """Write the list of authors to the destination file."""
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open("w", encoding="utf-8", newline="") as handle:
        for author in authors:
            handle.write(f"{author}\n")


def main() -> None:
    if not GUTENBERG_CSV.exists():
        error_message = (
            "Metadata CSV not found at {path}. "
            "Update the path if needed."
        ).format(path=GUTENBERG_CSV)
        raise FileNotFoundError(error_message)

    canonical_hash = compute_hash_from_list(CANONICAL_AUTHORS)

    metadata_stats = GUTENBERG_CSV.stat()
    cache_data: dict[str, object] = {}

    if CACHE_FILE.exists() and OUTPUT_FILE.exists():
        try:
            cache_data = json.loads(CACHE_FILE.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            cache_data = {}

        matches_canonical = cache_data.get("canonical_hash") == canonical_hash
        matches_meta_mtime = (
            cache_data.get("metadata_mtime") == metadata_stats.st_mtime
        )
        matches_meta_size = (
            cache_data.get("metadata_size") == metadata_stats.st_size
        )

        if matches_canonical and matches_meta_mtime and matches_meta_size:
            author_count = cache_data.get("author_count", 0)
            print(
                "Cached Gutenberg authors still valid "
                f"({int(author_count):,} entries). Skipping regeneration."
            )
            return

    rows = read_gutenberg_rows(GUTENBERG_CSV)
    authors = extract_unique_authors(rows)
    write_authors(authors, OUTPUT_FILE)

    output_hash = compute_hash_from_path(OUTPUT_FILE)
    cache_payload = {
        "canonical_hash": canonical_hash,
        "metadata_mtime": metadata_stats.st_mtime,
        "metadata_size": metadata_stats.st_size,
        "output_hash": output_hash,
        "author_count": len(authors),
    }
    CACHE_FILE.write_text(
        json.dumps(cache_payload, indent=2),
        encoding="utf-8",
    )

    message = f"Wrote {len(authors):,} authors to {OUTPUT_FILE}"
    print(message)


if __name__ == "__main__":
    main()
