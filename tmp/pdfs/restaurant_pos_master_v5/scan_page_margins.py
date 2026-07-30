from __future__ import annotations

from pathlib import Path

from PIL import Image


pages_dir = Path(__file__).resolve().parent / "pages"
rows: list[tuple[str, int, int, int, int]] = []
for path in sorted(pages_dir.glob("*.png")):
    image = Image.open(path).convert("L")
    content_mask = image.point(lambda value: 255 if value < 245 else 0)
    bounds = content_mask.getbbox()
    if bounds is None:
        rows.append((path.name, image.width, image.height, image.width, image.height))
        continue
    left, top, right, bottom = bounds
    rows.append(
        (path.name, left, top, image.width - right, image.height - bottom)
    )

minimums = tuple(min(row[index] for row in rows) for index in range(1, 5))
print(f"minimum margins (left, top, right, bottom): {minimums}")
print("pages with any margin below 40 px:")
for row in rows:
    if min(row[1:]) < 40:
        print(row)
