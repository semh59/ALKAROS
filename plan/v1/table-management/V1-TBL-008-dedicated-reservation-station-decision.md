# V1-TBL-008 - Dedicated reservation station: decision and task cluster

- Task ID: V1-TBL-008
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-04

## Goal

Semih'in onayıyla (2026-09-04): rezervasyon her işletmede aynı şekilde
işlemez — bazı işletmelerde ayrılmış rezervasyon personeli/ekranı olabilir,
bazılarında kasiyer yapar. `docs/domain/table-reservation-policy.md`'ye
ikinci bir `## Amendment` (2026-09-04) eklendi: kasiyerin kendi kat planı
ekranındaki mevcut, koşulsuz "Rezervasyon al" aksiyonuna ek olarak,
işletme başına açılabilir bir ayar (`reservations.dedicated_station_enabled`)
arkasında, müşteri ekranı gibi kendi URL'i olan sade bir "Rezervasyon
İstasyonu" ekranı (`/reservations`) sunulur. Hiçbir yeni izin kodu
eklenmedi — her iki ekran de aynı `tables.reserve` iznini ve sunucunun
zaten hesapladığı `AllowedCommands` listesini kullanır.

Ayrıca bu görevin araştırması sırasında gerçek, önceden var olan bir kusur
bulundu ve giderildi: `workspace.tsx`'teki `/`, `/tables`, `/billing`,
`/kitchen` rota izinleri hâlâ `pos.cashier.mutate`'i kontrol ediyordu —
bu izin migration 049'da (`V1-IAM-024`) kataloktan tamamen kaldırıldığı
için hiçbir oturum artık bu kontrolü hiç geçemiyordu. PosTerminal'de Satış
dışındaki HER ekran (Masalar/Hesap bölme/Mutfak) tüm oturumlar için erişilemez
haldeydi. Granüler karşılıklarına (`orders.create`/`tables.status`,
`bills.split`, `orders.send`) düzeltildi.

Bu görev kararı kaydeder ve uygulama görev kümesini kayıt altına alır:

| Görev | Kapsam |
| --- | --- |
| `V1-SET-003` | Ayarlar modülüne `reservations.dedicated_station_enabled` anahtarı (varsayılan kapalı) |
| `V1-CUI-006` | PosTerminal'e sade `/reservations` ekranı; `workspace.tsx`'in izin-kontrolü kusurunun düzeltilmesi |

## Owned surface

- `plan/v1/table-management/V1-TBL-008-dedicated-reservation-station-decision.md`
- `plan/v1/settings/V1-SET-003-reservation-station-toggle.md`
- `plan/v1/cashier-ui/V1-CUI-006-dedicated-reservation-station-screen.md`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır): `docs/domain/table-reservation-policy.md`
  (`V1-RMD-100` sahipliğinde kalır, yalnız kendi 2026-09-03 tarihli
  Amendment bölümü ve ilgili satırlar için) — yeni, ayrı ve açıkça
  tarihli ikinci bir `## Amendment (2026-09-04)` bölümü eklendi.
  V1-RMD-100'ün kendi 2026-09-03 tarihli bölümüne tek satırlık, açıkça
  tarihli bir "Superseded note (2026-09-04)" eklendi (izin kodu adının
  migration 049 ile değiştiğini kaydeder — kararın kendisi değişmedi);
  bölümün geri kalan metni değişmedi.
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- Karar: rezervasyon alımı, kasiyerin kendi ekranında (değişmeden) VEYA
  işletme isterse ayrı bir istasyon ekranında yapılabilir; hangisinin
  sunulacağı bir Ayarlar anahtarıyla, hangi rolün rezervasyon
  yapabileceği ise (değişmeyen) `tables.reserve` granüler izniyle
  belirlenir (yukarıdaki Amendment).
- İki uygulama görevinin kaydı (yukarıdaki tablo), her biri kendi modülünün
  dizininde.
- `workspace.tsx` izin-kontrolü kusurunun bulunması ve nedeninin
  kaydedilmesi (`V1-CUI-006`'nın kapsamında giderilir).

## Out of scope

- Uygulama kodu — iki çocuk görevin kapsamındadır.
- Own-check veya waiter/order servis-atama modeli — bu karar rezervasyonu
  hiç etkilemiyor (rezervasyon "kimin servis ettiği çek" ile ilgili değil).

## Dependencies

- V1-GOV-074

## Acceptance evidence

- `docs/domain/table-reservation-policy.md`: yeni `## Amendment
  (2026-09-04)` bölümü eklendi, Semih onaylı.
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata (yeni
  görev kaydı ve custody notu temiz).

## Handoff

- V1-SET-003
- V1-CUI-006
