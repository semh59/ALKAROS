"""Builds the Done-task ledger for the Payments slice: one JSON line per Done task whose owned surface touches it.

Run from the repository root: python evidence/V1-RMD-604/ledger.py
"""
import glob
import json
import re
import subprocess

SLICE = (
    "src/modules/payments", "src/host/dualscreen/dualscreenapplication.payments.cs",
    "src/host/experience/tokenterminalsettings", "tests/modules/payments",
    "src/host/experience/billing/paymentrefunds", "database/migrations",
)
MIGRATION_IDS = {"120", "121", "123", "126", "127", "131", "140", "141", "143", "158", "182", "183", "185", "186"}


def section(text, name):
    m = re.search(rf"^## {re.escape(name)}\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    return m.group(1) if m else ""


def touches_slice(path):
    p = path.lower().removesuffix("/**")
    if p.startswith("database/migrations"):
        return re.search(r"/(\d{3})-", p) and re.search(r"/(\d{3})-", p).group(1) in MIGRATION_IDS
    return any(p.startswith(s) for s in SLICE[:-1])


def commits_for(task_id):
    out = subprocess.run(
        ["git", "log", "--format=%h|%s", f"--grep={task_id}"], capture_output=True, text=True, encoding="utf-8"
    ).stdout
    return [line for line in out.splitlines() if line]


rows = []
for f in sorted(glob.glob("plan/**/*.md", recursive=True)):
    t = open(f, encoding="utf-8", errors="ignore").read()
    tid = re.search(r"^- Task ID: (\S+)", t, re.M)
    st = re.search(r"^- Status: (\S+)", t, re.M)
    if not (tid and st and st.group(1) == "Done"):
        continue
    owned = re.findall(r"`([^`]+)`", section(t, "Owned surface"))
    owned += re.findall(r"^- Sınırlı ek[^:]*: (\S+)", section(t, "Owned surface"), re.M)
    hit = [p for p in owned if p.startswith(("src/", "tests/", "database/")) and touches_slice(p)]
    if not hit:
        continue
    task_id = tid.group(1)
    evidence_dir = f"evidence/{task_id}"
    logs = glob.glob(f"{evidence_dir}/**/*.log", recursive=True)
    rows.append({
        "task": task_id,
        "file": f.replace("\\", "/"),
        "title": t.splitlines()[0].lstrip("# ").strip(),
        "slice_paths": hit,
        "acceptance": re.findall(r"^\d+\. (.+)$", section(t, "Acceptance evidence"), re.M),
        "commits": commits_for(task_id),
        "evidence_dir_exists": bool(glob.glob(evidence_dir)),
        "evidence_logs": len(logs),
    })

with open("evidence/V1-RMD-604/ledger.jsonl", "w", encoding="utf-8") as out:
    for r in rows:
        out.write(json.dumps(r, ensure_ascii=False) + "\n")
print(len(rows), "Done tasks in the Payments slice")
print("without evidence dir:", sum(1 for r in rows if not r["evidence_dir_exists"]))
print("without logs:", sum(1 for r in rows if r["evidence_logs"] == 0))
