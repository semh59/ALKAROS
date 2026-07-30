from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

import pypdfium2 as pdfium
from PIL import Image, ImageDraw
from pypdf import PdfReader


def pdf_object_id(value: Any) -> str | None:
    idnum = getattr(value, "idnum", None)
    generation = getattr(value, "generation", None)
    if idnum is None:
        return None
    return f"{idnum}:{generation or 0}"


def flatten_outline(items: list[Any], depth: int = 0) -> list[dict[str, Any]]:
    flattened: list[dict[str, Any]] = []
    for item in items:
        if isinstance(item, list):
            flattened.extend(flatten_outline(item, depth + 1))
            continue
        title = getattr(item, "title", str(item))
        flattened.append({"depth": depth, "title": title})
    return flattened


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: inspect_pdf.py INPUT_PDF OUTPUT_DIR")

    input_path = Path(sys.argv[1]).resolve()
    output_dir = Path(sys.argv[2]).resolve()
    pages_dir = output_dir / "pages"
    text_dir = output_dir / "text"
    pages_dir.mkdir(parents=True, exist_ok=True)
    text_dir.mkdir(parents=True, exist_ok=True)

    reader = PdfReader(str(input_path))
    if reader.is_encrypted:
        decrypt_result = reader.decrypt("")
        if not decrypt_result:
            raise RuntimeError("PDF is encrypted and cannot be opened with an empty password")

    try:
        outline = flatten_outline(reader.outline)
    except (AttributeError, KeyError, TypeError, ValueError) as exc:
        outline = [{"error": f"{type(exc).__name__}: {exc}"}]

    metadata = {str(key): str(value) for key, value in (reader.metadata or {}).items()}
    page_records: list[dict[str, Any]] = []
    all_text: list[str] = []

    for index, page in enumerate(reader.pages, start=1):
        text = page.extract_text() or ""
        (text_dir / f"page-{index:03d}.txt").write_text(text, encoding="utf-8")
        all_text.append(f"\n\n===== PAGE {index} =====\n\n{text}")

        resources = page.get("/Resources") or {}
        fonts = resources.get("/Font") or {}
        xobjects = resources.get("/XObject") or {}
        annotations = page.get("/Annots") or []
        media_box = page.mediabox

        page_records.append(
            {
                "page": index,
                "width_pt": float(media_box.width),
                "height_pt": float(media_box.height),
                "rotation": int(page.get("/Rotate", 0) or 0),
                "text_chars": len(text),
                "text_words": len(text.split()),
                "font_resources": sorted(str(key) for key in fonts.keys()),
                "xobject_count": len(xobjects),
                "annotation_count": len(annotations),
                "page_object": pdf_object_id(page.indirect_reference),
            }
        )

    (output_dir / "all_text.txt").write_text("".join(all_text), encoding="utf-8")

    pdf = pdfium.PdfDocument(str(input_path))
    if len(pdf) != len(reader.pages):
        raise RuntimeError(f"Renderer sees {len(pdf)} pages but pypdf sees {len(reader.pages)}")

    rendered_paths: list[Path] = []
    for index in range(len(pdf)):
        bitmap = pdf[index].render(scale=2.0, rotation=0)
        image = bitmap.to_pil().convert("RGB")
        page_path = pages_dir / f"page-{index + 1:03d}.png"
        image.save(page_path, format="PNG", optimize=True)
        rendered_paths.append(page_path)

    thumb_width = 420
    thumb_margin = 24
    label_height = 38
    per_sheet = 6
    sheets: list[str] = []
    for sheet_index, offset in enumerate(range(0, len(rendered_paths), per_sheet), start=1):
        batch = rendered_paths[offset : offset + per_sheet]
        thumbs: list[Image.Image] = []
        for page_path in batch:
            with Image.open(page_path) as original:
                ratio = thumb_width / original.width
                thumb = original.resize((thumb_width, round(original.height * ratio)))
                thumbs.append(thumb.copy())

        cell_height = max(image.height for image in thumbs) + label_height
        sheet = Image.new(
            "RGB",
            (2 * thumb_width + 3 * thumb_margin, 3 * cell_height + 4 * thumb_margin),
            "#d8d8d8",
        )
        draw = ImageDraw.Draw(sheet)
        for batch_index, thumb in enumerate(thumbs):
            row, column = divmod(batch_index, 2)
            x = thumb_margin + column * (thumb_width + thumb_margin)
            y = thumb_margin + row * (cell_height + thumb_margin)
            page_number = offset + batch_index + 1
            draw.text((x, y), f"Page {page_number}", fill="black")
            sheet.paste(thumb, (x, y + label_height))

        sheet_path = output_dir / f"contact-sheet-{sheet_index:02d}.png"
        sheet.save(sheet_path, format="PNG", optimize=True)
        sheets.append(str(sheet_path))

    report = {
        "input": str(input_path),
        "page_count": len(reader.pages),
        "encrypted": reader.is_encrypted,
        "metadata": metadata,
        "outline": outline,
        "form_field_count": len(reader.get_fields() or {}),
        "attachments": sorted((reader.attachments or {}).keys()),
        "pages": page_records,
        "contact_sheets": sheets,
    }
    (output_dir / "inspection.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
