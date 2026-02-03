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
    # Returns delta brace count for this line, updating lexical state.
    i = 0
    delta = 0
    while i < len(line):
        ch = line[i]
        nxt = line[i + 1] if i + 1 < len(line) else ""

        if state.in_line_comment:
            # rest of line ignored
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
                        # escaped quote in verbatim
                        i += 2
                        continue
                    # end verbatim string
                    state.in_string = False
                    state.in_verbatim_string = False
                    i += 1
                    continue
                i += 1
                continue

            # normal/interpolated string
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

        # normal code
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

    # line comment ends at EOL
    state.in_line_comment = False
    return delta, state


def main() -> None:
    path = Path(r"c:\p2p\SlskDown\MainForm.cs")
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines(True)

    state = State()
    level = 0
    min_level = 0
    min_line = 1
    last_change: list[tuple[int, int, int]] = []  # (line_no, level_after, delta)

    for idx, line in enumerate(lines, start=1):
        delta, state = scan_line(line, state)
        if delta != 0:
            level += delta
            last_change.append((idx, level, delta))
            if level < min_level:
                min_level = level
                min_line = idx

    print("final_level", level)
    print("min_level", min_level, "at_line", min_line)
    print("last_20_changes")
    for entry in last_change[-20:]:
        print(entry)

    # Show a window around where the level is highest near the end
    if last_change:
        last_line = last_change[-1][0]
        print("last_brace_change_line", last_line)


if __name__ == "__main__":
    main()
