from __future__ import annotations

import hashlib
import json
import subprocess
from pathlib import Path

CANDIDATE = "a03d02146961c29a8b847a7b0c472c6c8dd42c9f"
TREE = "39e9bb79d3d6f3e099e15a7ffafcc801f727843a"
REPO = Path(r"C:\Users\semih\AppData\Local\Temp\alkaros-v1-gov-003-20260824\repo")
ROOT = Path(r"D:\PROJECT\ALKAROS")
LEDGER = ROOT / "docs" / "audit" / "FULL_PROJECT_PRODUCTION_AUDIT_LEDGER.jsonl"
OUT = ROOT / "evidence" / "V1-GOV-003" / "ledger-verification.json"


def main() -> int:
    errors: list[str] = []
    entries = subprocess.check_output(
        ["git", "-C", str(REPO), "ls-tree", "-r", "-z", CANDIDATE]
    ).split(b"\0")
    expected: dict[str, str] = {}
    for entry in entries:
        if not entry:
            continue
        meta, raw_path = entry.split(b"\t", 1)
        expected[raw_path.decode("utf-8")] = meta.decode("ascii").split()[2]
    rows = [json.loads(line) for line in LEDGER.read_text(encoding="utf-8").splitlines() if line]
    seen: set[str] = set()
    for row in rows:
        path = row.get("path")
        if path in seen:
            errors.append(f"duplicate:{path}")
            continue
        seen.add(path)
        if path not in expected:
            errors.append(f"extra:{path}")
            continue
        data = (REPO / Path(path)).read_bytes()
        if row.get("candidate_commit") != CANDIDATE: errors.append(f"commit:{path}")
        if row.get("tree_sha") != TREE: errors.append(f"tree:{path}")
        if row.get("git_blob_sha") != expected[path]: errors.append(f"blob:{path}")
        if row.get("sha256") != hashlib.sha256(data).hexdigest(): errors.append(f"sha256:{path}")
        try:
            decoded = data.decode("utf-8")
            binary = b"\0" in data
        except UnicodeDecodeError:
            decoded, binary = "", True
        if row.get("is_binary") != binary: errors.append(f"binary:{path}")
        if binary:
            if row.get("line_start") is not None or row.get("line_end") is not None:
                errors.append(f"binary-lines:{path}")
            if data.startswith(b"\x1f\x8b") and row.get("binary_format") != "gzip":
                errors.append(f"binary-format:{path}")
        else:
            count = len(decoded.splitlines())
            start, end = ((1, count) if count else (0, 0))
            if row.get("line_start") != start or row.get("line_end") != end:
                errors.append(f"line-range:{path}")
        for key in ("file_class", "reviewer", "verdict", "evidence_ref"):
            if not row.get(key): errors.append(f"field-{key}:{path}")
    for missing in sorted(set(expected) - seen): errors.append(f"missing:{missing}")
    result = {
        "candidate_commit": CANDIDATE, "tree_sha": TREE,
        "expected_paths": len(expected), "ledger_rows": len(rows),
        "unique_paths": len(seen), "errors": len(errors), "error_details": errors[:100],
        "text_rows": sum(not row["is_binary"] for row in rows),
        "binary_rows": sum(row["is_binary"] for row in rows),
    }
    OUT.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
