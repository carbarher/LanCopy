from __future__ import annotations

from pathlib import Path


def main() -> None:
    path = Path(r"c:\p2p\SlskDown\MainForm.cs")
    data = path.read_bytes()

    n_null = data.count(b"\x00")
    n_cr = data.count(b"\r")
    n_lf = data.count(b"\n")
    n_crlf = data.count(b"\r\n")
    n_cr_only = n_cr - n_crlf
    n_lf_only = n_lf - n_crlf

    tail = data[-2000:] if len(data) > 2000 else data
    # last non-whitespace byte position (treat 0x00 as whitespace for this check)
    whitespace = set(b" \t\r\n\x00")
    last_non_ws = None
    for i in range(len(data) - 1, -1, -1):
        if data[i] not in whitespace:
            last_non_ws = i
            break

    print("path", str(path))
    print("bytes", len(data))
    print("null_bytes", n_null)
    print("cr", n_cr, "lf", n_lf, "crlf", n_crlf, "cr_only", n_cr_only, "lf_only", n_lf_only)
    # newline terminators count approximation
    print("newline_terms", n_crlf + n_cr_only + n_lf_only)
    if last_non_ws is None:
        print("last_non_ws", None)
    else:
        print("last_non_ws_offset", last_non_ws, "last_non_ws_byte", hex(data[last_non_ws]))
        print("tail_preview_ascii", tail.decode("utf-8", errors="replace")[-400:])


if __name__ == "__main__":
    main()
