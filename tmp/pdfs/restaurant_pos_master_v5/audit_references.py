from __future__ import annotations

import re
from collections import defaultdict
from pathlib import Path


TOKEN = re.compile(r"(?<![A-Z0-9])(?:IV|III|II|I)\.\d+(?:\.\d+)?[A-Z]?")
HEADING = re.compile(r"^((?:IV|III|II|I)\.\d+(?:\.\d+)?[A-Z]?)(?:\s|$)")


def main() -> None:
    text_dir = Path(__file__).resolve().parent / "text"
    headings: dict[str, list[int]] = defaultdict(list)
    references: dict[str, set[int]] = defaultdict(set)

    for path in sorted(text_dir.glob("page-*.txt")):
        page = int(path.stem.split("-")[1])
        content = path.read_text(encoding="utf-8")
        for line in content.splitlines():
            match = HEADING.match(line.strip())
            if match:
                headings[match.group(1)].append(page)
            for token in TOKEN.findall(line):
                references[token].add(page)

    unresolved = {
        token: sorted(pages)
        for token, pages in sorted(references.items())
        if token not in headings
    }
    duplicates = {
        token: pages for token, pages in sorted(headings.items()) if len(pages) > 1
    }
    print(f"HEADINGS={len(headings)} REFERENCES={len(references)}")
    print("UNRESOLVED")
    for token, pages in unresolved.items():
        print(f"{token}: {pages}")
    print("DUPLICATE_HEADINGS")
    for token, pages in duplicates.items():
        print(f"{token}: {pages}")


if __name__ == "__main__":
    main()
