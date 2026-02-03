from __future__ import annotations

import re
import unicodedata
from pathlib import Path


_CONFUSABLES = str.maketrans(
    {
        "Α": "A",
        "Β": "B",
        "Ε": "E",
        "Ζ": "Z",
        "Η": "H",
        "Ι": "I",
        "Κ": "K",
        "Μ": "M",
        "Ν": "N",
        "Ο": "O",
        "Ρ": "P",
        "Τ": "T",
        "Υ": "Y",
        "Χ": "X",
        "а": "a",
        "е": "e",
        "о": "o",
        "р": "p",
        "с": "c",
        "х": "x",
        "у": "y",
        "і": "i",
        "Α": "A",
        "О": "O",
        "І": "I",
        "а": "a",
        "А": "A",
        "В": "B",
        "Е": "E",
        "К": "K",
        "М": "M",
        "Н": "H",
        "О": "O",
        "Р": "P",
        "С": "C",
        "Т": "T",
        "Х": "X",
        "У": "Y",
        "Ь": "b",
        "І": "I",
        "Ј": "J",
        "ј": "j",
        "ϲ": "c",
        "ο": "o",
    }
)


def _strip_invisibles(text: str) -> str:
    text = "".join(ch for ch in text if unicodedata.category(ch) not in {"Cf", "Cc"})
    return text


def author_key(author: str) -> str:
    cleaned = _strip_invisibles(author)
    cleaned = unicodedata.normalize("NFKC", cleaned)
    cleaned = cleaned.translate(_CONFUSABLES)
    cleaned = unicodedata.normalize("NFKD", cleaned)
    cleaned = "".join(ch for ch in cleaned if not unicodedata.combining(ch))
    cleaned = re.sub(r"[^0-9A-Za-z]+", " ", cleaned)
    cleaned = re.sub(r"\s+", " ", cleaned).strip().casefold()
    return cleaned


root = Path(__file__).resolve().parents[1]
canonical_path = root / "Data" / "canonical_authors_priority.txt"
raw_lines = canonical_path.read_text(encoding="utf-8", errors="ignore").splitlines()
lines = [line.strip() for line in raw_lines if line.strip()]

seen: set[str] = set()
unique: list[str] = []
for line in lines:
    key = author_key(line)
    if not key:
        continue
    if key in seen:
        continue
    seen.add(key)
    unique.append(line)

canonical_path.write_text("\n".join(unique) + "\n", encoding="utf-8")

count = len(unique)
print(count)

output_path = root / "Data" / "canonical_count.txt"
output_path.write_text(str(count), encoding="utf-8")
