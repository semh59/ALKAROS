# V1-RMD-234 - Ekran koruyucu üzerindeki marka rozetinin kontrast koruması

- Task ID: V1-RMD-234
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Goal

`CustomerDisplay.tsx`'in ekran koruyucu (Idle) görünümünde, gizlilik notu
(`.idle-screensaver-note`) için işletmenin yüklediği görselin/videonun
arkasında sabit bir kontrast katmanı (`rgba(13, 21, 29, .78)`) var, ama
ALKAROS marka rozeti (`<DisplayBrand />`, `.display-brand`) için AYNI
koruma YOK — doğrudan görsel/video üzerine biniyor, hiçbir arka
plan/gölge katmanı içermiyor. Bağımsız bir denetim ajanı (2026-09-17, Kasa
modülü kapsamlı denetimi) bunu tespit etti: işletme açık renkli bir görsel
yüklerse, rozet tamamen okunaksız/görünmez hâle gelebilir.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/styles.css
  (V1-CUI-009 sahipliğinde kalır) — yalnız ekran koruyucu bağlamındaki
  `.display-brand` için (ör. `.idle-screen-with-image .display-brand`
  gibi kapsamlı bir seçiciyle) bir arka plan/gölge/blur katmanı eklenir;
  foundations.md'nin renk/kontrast kurallarını ihlal etmez, diğer
  `.display-brand` kullanımları (pairing/unavailable/completed ekranları)
  değişmez.
- `evidence/V1-RMD-234/**`

## In scope

- Yalnız ekran koruyucu gösterilirken (`idle-screen-with-image` sınıfı
  aktifken) marka rozetine, gizlilik notuyla tutarlı bir kontrast koruması
  (yarı saydam koyu panel, `text-shadow` veya benzeri) eklemek.

## Out of scope

- Gizlilik notunun kendi stilini değiştirmek — zaten doğru.
- Ekran koruyucu dışındaki hiçbir `.display-brand` kullanımı.

## Dependencies

- V1-CDP-002

## Acceptance evidence

- Görsel doğrulama (Semih'in kendi tarayıcısında, bu ortamda ekran
  görüntüsü alabilen bir araç olmadığı için): açık renkli bir görsel
  yüklenip rozetin hâlâ okunabilir kaldığı doğrulanır.
- `corepack pnpm build` (PosTerminal) → 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
