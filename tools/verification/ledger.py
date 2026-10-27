"""Builds the ledger of every Done task: owned surface, acceptance items, commits, evidence logs and flags.

Run from the repository root: python tools/verification/ledger.py
Output: evidence/V1-RMD-739/ledger.jsonl
"""
import glob
import json
import re
import subprocess
from collections import defaultdict

OUT = "evidence/V1-RMD-739/ledger.jsonl"


def section(text, name):
    m = re.search(rf"^## {re.escape(name)}\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    return m.group(1) if m else ""


def module_of(paths):
    for p in paths:
        m = re.match(r"(?:src/Modules|tests/Modules)/([^/]+)/", p)
        if m:
            return m.group(1)
        m = re.match(r"src/Clients/([^/]+)/", p)
        if m:
            return "Client:" + m.group(1)
        if p.startswith("src/Host/"):
            return "Host"
        if p.startswith("database/"):
            return "Database"
    return "Plan/Other"


def commits_by_task():
    out = subprocess.run(["git", "log", "--format=%h|%s"], capture_output=True, text=True, encoding="utf-8").stdout
    index = defaultdict(list)
    for line in out.splitlines():
        for tid in set(re.findall(r"V\d+[A-Z]?-[A-Z]+-\d+", line)):
            index[tid].append(line)
    return index


commits = commits_by_task()
rows = []
for f in sorted(glob.glob("plan/**/*.md", recursive=True)):
    t = open(f, encoding="utf-8", errors="ignore").read()
    tid = re.search(r"^- Task ID: (\S+)", t, re.M)
    st = re.search(r"^- Status: (\S+)", t, re.M)
    if not (tid and st and st.group(1) == "Done"):
        continue
    task_id = tid.group(1)
    owned = re.findall(r"`([^`]+)`", section(t, "Owned surface"))
    acceptance = re.findall(r"^(?:\d+\.|-) (.+)$", section(t, "Acceptance evidence"), re.M)
    logs = glob.glob(f"evidence/{task_id}/**/*.log", recursive=True)
    flags = []
    if not glob.glob(f"evidence/{task_id}"):
        flags.append("NO_EVIDENCE_DIR")
    elif not logs:
        flags.append("NO_LOGS")
    if not acceptance:
        flags.append("EMPTY_ACCEPTANCE")
    if not commits.get(task_id):
        flags.append("NO_COMMITS")
    rows.append({
        "task": task_id,
        "file": f.replace("\\", "/"),
        "title": t.splitlines()[0].lstrip("# ").strip(),
        "module": module_of(owned),
        "family": re.sub(r"-\d+$", "", task_id),
        "owned": owned,
        "acceptance_count": len(acceptance),
        "commits": commits.get(task_id, []),
        "evidence_logs": len(logs),
        "flags": flags,
        "decision": None,
    })

with open(OUT, "w", encoding="utf-8") as out:
    for r in rows:
        out.write(json.dumps(r, ensure_ascii=False) + "\n")

by_module = defaultdict(int)
for r in rows:
    by_module[r["module"]] += 1
print(len(rows), "Done tasks")
for k, v in sorted(by_module.items(), key=lambda kv: -kv[1]):
    print(f"  {k}: {v}")
for flag in ("NO_EVIDENCE_DIR", "NO_LOGS", "EMPTY_ACCEPTANCE", "NO_COMMITS"):
    print(flag, sum(1 for r in rows if flag in r["flags"]))
