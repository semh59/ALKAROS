# V1-CUI-009 - PosTerminal paylaşılan design-system token'larını Faz 0'a taşı

- Task ID: V1-CUI-009
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`src/Clients/PosTerminal/src/design-system/tokens.css` içindeki Faz 0
öncesi palet (`--ds-color-ink: #17212b`, `--ds-color-brand: #283a4a`,
`--ds-color-accent: #b73c20` vb.) `docs/design/foundations.md`'nin kilitli
Faz 0 renklerine taşımak ve `src/routes/Cashier.tsx` ile onun CSS'ini bu
geçişten sonra görsel/kontrast olarak doğrulamak. Bu dosya PosTerminal'in
tüm route'ları tarafından paylaşıldığı için değişiklik bilerek tüm
uygulamaya yayılır (V1-CUI-007 kararı, §4.2 ve §6).

## Owned surface

- `src/Clients/PosTerminal/src/design-system/tokens.css`
- `src/Clients/PosTerminal/src/styles.css`
- `src/Clients/PosTerminal/src/routes/Cashier.tsx` (yalnız görsel/kontrast
  doğrulaması gereken sınıf düzeltmeleri; akış/state mantığı değişmez)
- `src/Clients/PosTerminal/src/design-system/primitives.test.tsx`
- `evidence/V1-CUI-009/**`
- Bu görev, `design-system/Icon.tsx`, `icon.css`, `index.ts` ve
  `primitives.tsx`/`primitives.css`'e dokunmaz (V1-RMD-016 sahipliğinde
  kalır); diğer PosTerminal route dosyalarına da dokunmaz.

## In scope

- Token değerlerinin Faz 0 paletine geçişi.
- `--ds-color-accent` dolgu/metin kontrast kuralının foundations.md §1.1
  ile birebir doğrulanması (WCAG hesabı).
- Geçişin PosTerminal'in diğer route'larını (Tables, Catalog, Billing,
  Kitchen-operations, Authorization-decisions, System-health,
  Reservation-station) kırmadığının regresyon kontrolü.

## Out of scope

- Bu route'ların kendi bileşen/akış tasarımı (ayrı modül görevleri).
- V1.3 CashSession/tender UI (backend henüz `Planned`).

## Dependencies

- V1-CUI-007

## Acceptance evidence

- `npm run build` (tsc --noEmit + vite build) → 0 hata, tüm chunk'lar üretildi.
- `npx vitest run` → 23/23 dosya, 175/175 test geçti (a11y/axe testi dahil —
  yeni bir kontrast ihlali yok).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Değişiklik yalnız `tokens.css` + `styles.css` (git diff ile doğrulandı);
  `Cashier.tsx` ve `primitives.test.tsx` incelendi, ikisinde de sınıf adı
  dışında renk mantığı olmadığı için düzeltme gerekmedi.
- Yapılanlar: `tokens.css`'te ink/brand/accent/canvas Faz 0'a taşındı
  (success/warning/danger/info zaten birebir aynıydı, dokunulmadı);
  `--accent-rgb` kanal üçlüsü yeni accent'e güncellendi (aksi hâlde glow/gölge
  efektleri eski turuncu tonda kalırdı); accent dolgu + beyaz metin ikilisi
  bulunan her yer (`.primary`, `.brand-mark`, `.category-rail button.active`,
  `.display-brand > span`, `.display-retry`) ink metne çevrildi
  (foundations.md §1.1, 1.85:1 FAIL → 8.86:1 AAA); accent'in düz metin/ikon
  rengi olarak kullanıldığı ışık zeminler (`.login-card-heading > span`,
  `.new-order-icon`) aynı şekilde düzeltildi; eski accent'in elle yazılmış
  hex kopyaları (`#bd3f23`, `#b53c22`, `#a6432d`, `#a9432d`, `#b93e23` —
  Cashier'ın fiyat/toplam/kicker metinleri ve Customer Display'in toplam
  kartı) `--navy`/`--ink`'e taşındı.

**Bilerek dokunulmayan (ayrı görev, kapsam dışı):** Login ve Customer
Display ekranlarının genel koyu tema/hero tasarımı — foundations.md §4
"hiçbir ekran koyu tema kullanmaz" kuralına aykırı ama bu, tek tek token
değiştirmekle değil o iki ekranın kendi bileşen/akış tasarımının yeniden
ele alınmasıyla çözülür (bu görevin "Out of scope" maddesi). `--ds-font-sans`/
`--ds-font-mono` (Manrope/DM Mono) de aynı sebeple — foundations.md §2 tek
aile Inter istiyor ama webfont değişimi `index.html`'e dokunmayı gerektiriyor,
bu görevin allowlist'i dışında (V1-CUI-008'de Cashier için verilen kararla
aynı gerekçe).
