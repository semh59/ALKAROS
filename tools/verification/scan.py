"""Repository-wide deterministic scans for fake code, debt and weak algorithms.

Run from the repository root: python tools/verification/scan.py
Output: evidence/V1-RMD-739/scans/<name>.txt plus a summary on stdout.
"""
import os
import re
import subprocess

OUT = "evidence/V1-RMD-739/scans"
os.makedirs(OUT, exist_ok=True)

files = subprocess.run(["git", "ls-files", "src", "tests", "database", "tools"],
                       capture_output=True, text=True, encoding="utf-8").stdout.splitlines()
CODE = (".cs", ".ts", ".tsx", ".js", ".mjs", ".sql", ".py")


def is_test(path):
    return path.startswith("tests/") or ".test." in path or path.endswith("Tests.cs")


SCANS = {
    "fake-or-unfinished": (r"NotImplementedException|\b(TODO|FIXME|HACK|XXX)\b|\b(STUB|SIMULATED|MOCKED)\b", False),
    "inmemory-or-fake-in-production": (r"\b(InMemory|Fake|Stub|Dummy)[A-Za-z]*\b", False),
    "swallowed-exception": (r"catch\s*(\([^)]*\))?\s*\{\s*\}", False),
    "suppressed-analyzer": (r"#pragma warning disable|SuppressMessage|eslint-disable|@ts-ignore|@ts-nocheck|oxlint-disable", False),
    "skipped-test": (r"Skip\s*=|\.skip\(|\bxit\(|\bxdescribe\(|\[Fact\(Skip", True),
    "commented-out-code": (r"^\s*//\s*(var|if|for|foreach|return|await|public|private|using)\b[^/]*[;{}]\s*$", False),
    "db-call-in-loop": (r"foreach\s*\(.*\)\s*\{?\s*$", False),
    "unbounded-query": (r"SELECT\s[^;]*\sFROM\s[^;]*;", False),
    "float-money": (r"\b(double|float)\s+\w*(amount|price|total|tutar|fiyat|balance)\w*", False),
    "datetime-now": (r"DateTime(Offset)?\.(Now|UtcNow)\b|Date\.now\(\)", False),
    "raw-error-message-in-ui": (r"\.message\b", False),
}

summary = []
for name, (pattern, include_tests) in SCANS.items():
    rx = re.compile(pattern, re.I if name in ("unbounded-query",) else 0)
    hits = []
    for path in files:
        if not path.endswith(CODE) or (is_test(path) and not include_tests):
            continue
        if name == "raw-error-message-in-ui" and not path.startswith("src/Clients/"):
            continue
        if name == "unbounded-query" and not path.endswith(".cs"):
            continue
        try:
            lines = open(path, encoding="utf-8", errors="ignore").read().splitlines()
        except OSError:
            continue
        for i, line in enumerate(lines, 1):
            if not rx.search(line):
                continue
            if name == "unbounded-query":
                window = " ".join(lines[max(0, i - 1):i + 12])
                if re.search(r"LIMIT|WHERE|COUNT\(|SUM\(|MAX\(|MIN\(|EXISTS|INSERT|UPDATE|DELETE", window, re.I):
                    continue
            if name == "db-call-in-loop":
                window = "\n".join(lines[i:i + 8])
                if not re.search(r"await\s+[\w.]*(Repository|Store|command|Command|dataSource|DataSource)[\w.]*\.\w*Async", window):
                    continue
            hits.append(f"{path}:{i}: {line.strip()[:160]}")
    with open(f"{OUT}/{name}.txt", "w", encoding="utf-8") as f:
        f.write("\n".join(hits))
    summary.append((name, len(hits)))
    print(f"{name}: {len(hits)}")
