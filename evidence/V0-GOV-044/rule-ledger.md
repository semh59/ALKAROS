# V0-GOV-044 — before/after rule ledger

Ölçüm komutu: `npx markdownlint-cli2@0.23.2` (config `.markdownlint-cli2.jsonc`, globs: `plan/** docs/** evidence/**
AGENTS.md`)

## Before (2026-08-13, HEAD `c9e1961` öncesi canlı ölçüm)

| Kural | Adet |
| --- | --- |
| MD013/line-length | 120 |
| MD060/table-column-style | 66 |
| MD040/fenced-code-language | 46 |
| MD032/blanks-around-lists | 12 |
| MD047/single-trailing-newline | 9 |
| MD004/ul-style | 7 |
| MD036/no-emphasis-as-heading | 6 |
| MD031/blanks-around-fences | 5 |
| MD022/blanks-around-headings | 2 |
| MD038/no-space-in-code | 2 |
| MD058/blanks-around-tables | 1 |
| MD056/table-column-count | 1 |
| MD009/no-trailing-spaces | 1 |
| MD033/no-inline-html | 1 |
| MD025/single-title | 1 |
| MD034/no-bare-urls | 1 |
| **Toplam** | **281** |

Etkilenen dosya sayısı: **74**.

## After (2026-08-13)

| Kural | Adet |
| --- | --- |
| tümü | 0 |
| **Toplam** | **0** |

Ölçülen dosya: **514** (0 issues in 0 files), exit 0 — `markdownlint_final.txt`.

## Düzeltme eşlemesi

- MD013, MD004, MD032, MD031, MD022, MD047, MD009, MD038, MD058, MD034: `--fix` + 120-karakter sarma
  (fence/tablo bilinçli script)
- MD060: `markdownlint-cli2 --fix` (compact style)
- MD040: açılış fence'lerine `console` dili (`md040_fix.py`, 46 blok)
- MD036: `**"..."**` → düz metin (6 satır)
- MD025: ikinci `#` başlığı `##`'e indirme (`V1-IAM-002/closure-2026-08-08.md`)
- MD033: `<proj>` backtick'e alındı (`ENV-003/env003-test-matrix.md`)
- MD056: hücre içi literal pipe karakteri backslash ile escape edildi (`plan/TRACEABILITY.md` C46)
