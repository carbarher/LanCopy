from __future__ import annotations

from pathlib import Path


def main() -> None:
    path = Path(r"c:\p2p\SlskDown\MainForm.cs")
    text = path.read_text(encoding="utf-8", errors="replace")

    counts = {
        "\\u2028": text.count("\u2028"),  # LINE SEPARATOR
        "\\u2029": text.count("\u2029"),  # PARAGRAPH SEPARATOR
        "\\u0085": text.count("\u0085"),  # NEXT LINE
    }

    # regular newlines
    counts["\\n"] = text.count("\n")
    counts["\\r"] = text.count("\r")

    print("file", str(path))
    for k, v in counts.items():
        print(k, v)

    extra = counts["\\u2028"] + counts["\\u2029"] + counts["\\u0085"]
    print("unicode_newline_total", extra)


if __name__ == "__main__":
    main()
