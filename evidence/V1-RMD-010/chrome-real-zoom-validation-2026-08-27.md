# V1-RMD-010 real Chrome zoom validation

- Date: 2026-08-27
- Surface: authenticated production `PosTerminal` table management
- Runtime: real HTTPS Host in `Production` with fresh digest-pinned PostgreSQL 18 and all 37 V1 migrations
- Browser console errors/warnings: `0`
- Verdict: **FAIL**

## 200 percent

The normalized device-pixel ratio changed from approximately `1.0` to `2.0`, proving real 200% page zoom. The
effective viewport was `195x422` CSS pixels. Document client/scroll widths were both `187`, so no horizontal scroll
was exposed. All rendered form controls were at least `54.06x44` CSS pixels.

The header nevertheless overflowed its visible boundary. `Müşteri ekranı` ended at x=`196.38` while the visible
client boundary was x=`187`; `Çıkış` occupied x=`204.38..265.91` and was completely invisible. Because the document
reported no horizontal overflow, the clipped controls could not be reached by horizontal scrolling.

Evidence: `chrome-real-zoom-200-header-clipping-2026-08-27.png`.

## 400 percent

The real user-window device-pixel ratio changed from the restored baseline `3` to `12`, proving a 4x page zoom. The
effective viewport was `320x123` CSS pixels. Document client/scroll widths were both `316`; all rendered form controls
remained at least `53.90x44` CSS pixels.

The production shell failed reflow vertically. The banner covered y=`0..68`, system status covered y=`7.75..55.75`,
and primary navigation covered y=`55.75..123.75`. These fixed regions overlapped and consumed the entire viewport,
leaving no visible main-content area. A cashier cannot visually complete the table workflow at this zoom level.

Evidence: `chrome-real-zoom-400-tables-2026-08-27.png`.

## Restoration

Chrome was restored to 100% page zoom. The final device-pixel ratio was `3`, exactly one quarter of the measured 400%
value, and the viewport returned from `320x123` to `1280x495` CSS pixels. The final document client/scroll widths were
both `1265` with no horizontal overflow.

Evidence: `chrome-real-zoom-100-restored-2026-08-27.png` and
`chrome-real-zoom-validation-2026-08-27.json`.

## Required remediation

This is a production-source finding outside the evidence-only `V1-RMD-010` owned surface. A separate exact-custody
remediation must make header actions reflow without clipping at 200% and prevent fixed banner/status/navigation regions
from consuming or covering the main viewport at 400%, while preserving the 44x44 CSS-pixel target minimum.
