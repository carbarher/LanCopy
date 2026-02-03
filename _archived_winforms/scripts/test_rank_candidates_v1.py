from __future__ import annotations

import ctypes
import json
from pathlib import Path


def _find_dll() -> Path:
    root = Path(__file__).resolve().parents[1]
    candidates = [
        root / "bin" / "Release" / "net8.0-windows" / "slskdown_core.dll",
        root / "bin" / "Debug" / "net8.0-windows" / "slskdown_core.dll",
        root / "rust_core" / "target" / "release" / "slskdown_core.dll",
        root / "rust_core" / "target" / "debug" / "slskdown_core.dll",
        root / "RustCore" / "target" / "release" / "slskdown_core.dll",
        root / "RustCore" / "target" / "debug" / "slskdown_core.dll",
    ]
    for path in candidates:
        if path.exists():
            return path
    msg = "slskdown_core.dll not found in expected locations"
    raise FileNotFoundError(msg)


def main() -> None:
    dll_path = _find_dll()
    lib = ctypes.CDLL(str(dll_path))

    lib.rank_candidates_v1.argtypes = [ctypes.c_char_p]
    lib.rank_candidates_v1.restype = ctypes.c_void_p

    lib.free_rust_string.argtypes = [ctypes.c_void_p]
    lib.free_rust_string.restype = None

    request = {
        "SchemaVersion": 1,
        "Candidates": [
            {
                "File": {
                    "FileName": "Borges - Ficciones (ESP).pdf",
                    "Extension": ".pdf",
                    "Username": "user1",
                    "FolderPath": "",
                    "SizeBytes": 5_000_000,
                    "UploadSpeed": 0,
                    "QueueLength": 0,
                    "FreeUploadSlots": 1,
                    "Author": "Jorge Luis Borges",
                    "Network": "Soulseek",
                },
                "TargetAuthor": "Jorge Luis Borges",
                "TargetTitle": "Ficciones",
            }
        ],
    }

    payload = json.dumps(request, ensure_ascii=False).encode("utf-8")
    out_ptr = lib.rank_candidates_v1(payload)
    if not out_ptr:
        raise RuntimeError("rank_candidates_v1 returned null")

    try:
        out_json = ctypes.string_at(out_ptr).decode("utf-8", errors="replace")
    finally:
        lib.free_rust_string(out_ptr)

    response = json.loads(out_json)

    if response.get("SchemaVersion") != 1:
        raise AssertionError("SchemaVersion mismatch")

    results = response.get("Results")
    if not isinstance(results, list) or len(results) != 1:
        raise AssertionError("Results list missing")

    item = results[0]
    if "Score" not in item or "Reasons" not in item or "File" not in item:
        raise AssertionError("Missing expected fields")

    if not isinstance(item["Reasons"], list):
        raise AssertionError("Reasons must be a list")

    score = float(item["Score"])
    if not (0.0 <= score <= 100.0):
        raise AssertionError("Score out of range")

    print("OK")


if __name__ == "__main__":
    main()
