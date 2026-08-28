# V1-RMD-010 gerçek durum transcriptleri — 2026-08-27

Candidate: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`  
Tree: `39e9bb79d3d6f3e099e15a7ffafcc801f727843a`  
Host: `https://localhost:58299`  
Database: disposable PostgreSQL 18 digest-pinned container; 37 V1 migrations applied.

## Gerçek akışlar

- Cashier login succeeded and rendered the production `PosTerminal` shell (`ALKAROS`, `Kasa satış`, `Sunucu hazır`).
- A draft order was opened, `Espresso` was added, and the visible total became `₺93,50`; the 390px sticky `Gönder`
  CTA remained visible.
- A table order was opened from `Masalar` after the previous terminal draft was submitted. Product add and submit
  returned the real success status `Sipariş gönderildi`, revision `3`.
- `Masalar` exposed zone/table filters, availability states, table context, and `Masada sipariş aç`; `Menü` exposed
  product/category/tax/modifier/price management; `Mutfak` exposed production API tickets and transition controls.
- A real customer-display code paired successfully with the cashier. After polling, the display rendered
  `Sipariş kasadan gönderildi`, the item and payable total. Revoke returned the display to a fresh pairing code after
  bounded polling.

## Hata, retry ve connectivity

- Invalid eight-character pairing code `ZZZZZZZZ` returned the real alert `İstenen kayıt bulunamadı.` while keeping the
  dialog and retryable pairing controls open.
- Host was stopped and the cashier health indicator changed to `Bağlantı sorunu`; the existing submitted order stayed
  read-only and was not mutated.
- During the same controlled outage, the customer display rendered `BAĞLANTI KURULAMADI`, `Ekran bilgisi alınamadı`
  and `Tekrar dene`. After Host restart, clicking `Tekrar dene` returned it to a fresh pairing code.
- A second attempt to open a table order while the same terminal still owned a draft returned the real conflict alert
  `Kayıt başka bir işlem tarafından değiştirildi.` This is the expected terminal active-order fencing path, not a silent
  relocation.

## Modal/focus transcript

At 390×844, `Ekranı eşleştir` opened a named `role=dialog` with `aria-modal="true"` and heading `Ekran bağlantısını
yönetin`. Focus initially landed on the close button. Clicking the pairing textbox then `Shift+Tab` moved focus back to
the close button. Pressing `Escape` removed the dialog and restored focus to the trigger (`Ekranı eşleştir`).

The full accessibility snapshot is captured in the browser transcript used to produce this file; representative tree:

```text
- dialog "Ekran bağlantısını yönetin":
  - heading "Ekran bağlantısını yönetin" [level=2]
  - button "Eşleştirme penceresini kapat" [active]
  - textbox "Eşleştirme kodu"
  - button "Vazgeç"
  - button "Ekranı eşleştir" [disabled]
  - button "Aktif ekran yetkisini kaldır"
```

## State coverage disposition

| State | Evidence | Result |
| --- | --- | --- |
| loading / initial auth | fresh login navigation and DOM snapshots | observed; transient, no indefinite spinner |
| empty draft | real draft order with `0 ürün`, `₺0,00`, disabled send | observed |
| busy | source exposes `Sipariş hazırlanıyor…` / `Eşleştiriliyor…`; the local bridge completed requests too quickly to capture an in-flight frame | not independently timed; no pass claimed |
| success / submitted | order submit transcript and `order-submitted-390x844-2026-08-27.png` | observed |
| error / retry | invalid pairing alert and customer-display outage/retry | observed |
| offline / reconnect | controlled Host stop/restart transcript | observed |
| stale / conflict | terminal order conflict alert; stale-money and reconnect evidence in `evidence/V1-RMD-020/` | observed across fresh integrated run |
| unauthorized | anonymous interstitial and authenticated login gate from `evidence/V1-RMD-020/` | observed; no bypass |
| Paying / Completed / Unavailable | real state fixtures and fresh integrated-state evidence in `evidence/V1-RMD-020/` | inherited unchanged source evidence; no new claim beyond that evidence |

## Artefacts

- `responsive-matrix-2026-08-27.json`
- `dom-a11y-snapshots-2026-08-27.md`
- `a11y-focus-and-tooling-2026-08-27.md`
- `cashier-*.png`, `breakpoint-*.png`, `tables-boundary-*.png`, `catalog-boundary-*.png`, `kitchen-boundary-*.png`
- `cashier-active-order-390x844-2026-08-27.png`
- `pairing-modal-390x844-2026-08-27.png`, `pairing-error-390x844-2026-08-27.png`
- `display-active-390x844-2026-08-27.png`, `display-pairing-after-revoke-390x844-2026-08-27.png`
- `display-retry-error-390x844-2026-08-27.png`, `display-retry-recovered-390x844-2026-08-27.png`
