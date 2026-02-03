from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass
class State:
    in_line_comment: bool = False
    in_block_comment: bool = False
    in_string: bool = False
    in_verbatim_string: bool = False
    in_char: bool = False
    escape: bool = False


def scan_line(line: str, state: State) -> tuple[int, State]:
    i = 0
    delta = 0
    while i < len(line):
        ch = line[i]
        nxt = line[i + 1] if i + 1 < len(line) else ""

        if state.in_line_comment:
            break

        if state.in_block_comment:
            if ch == "*" and nxt == "/":
                state.in_block_comment = False
                i += 2
                continue
            i += 1
            continue

        if state.in_string:
            if state.in_verbatim_string:
                if ch == '"':
                    if nxt == '"':
                        i += 2
                        continue
                    state.in_string = False
                    state.in_verbatim_string = False
                    i += 1
                    continue
                i += 1
                continue

            if state.escape:
                state.escape = False
                i += 1
                continue
            if ch == "\\":
                state.escape = True
                i += 1
                continue
            if ch == '"':
                state.in_string = False
                i += 1
                continue
            i += 1
            continue

        if state.in_char:
            if state.escape:
                state.escape = False
                i += 1
                continue
            if ch == "\\":
                state.escape = True
                i += 1
                continue
            if ch == "'":
                state.in_char = False
                i += 1
                continue
            i += 1
            continue

        if ch == "/" and nxt == "/":
            state.in_line_comment = True
            break
        if ch == "/" and nxt == "*":
            state.in_block_comment = True
            i += 2
            continue

        if ch == '@' and nxt == '"':
            state.in_string = True
            state.in_verbatim_string = True
            i += 2
            continue

        if ch == '"':
            state.in_string = True
            state.in_verbatim_string = False
            i += 1
            continue

        if ch == "'":
            state.in_char = True
            i += 1
            continue

        if ch == "{":
            delta += 1
        elif ch == "}":
            delta -= 1

        i += 1

    state.in_line_comment = False
    return delta, state


def main() -> None:
    path = Path(r"c:\p2p\SlskDown\MainForm.cs")
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()

    start = 31780
    end = 32220

    state = State()
    level = 0
    for idx, line in enumerate(lines, start=1):
        delta, state = scan_line(line + "\n", state)
        if delta != 0:
            level += delta
        if start <= idx <= end and (delta != 0 or idx in (start, 32005, 32138, 32139, 32140, end)):
            print(f"{idx}: level={level:4d} delta={delta:+d} | {line[:120]}")

    print("final_level", level)


if __name__ == "__main__":
    main()
