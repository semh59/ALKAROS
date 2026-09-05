# V1-IAM-026 - Grant-class bill adjustment surface: decision and task cluster

- Task ID: V1-IAM-026
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-04

## Goal

`V1-IAM-025` denetiminde bulundu: `bills.void`, `bills.comp` ve
`bills.discount` — modelin (§3) TEK grant-class permission ailesi — hiçbir
canlı HTTP endpoint'ine bağlı değildi, ve void'in domain modeli "gönderilmiş
kalem asla void edilemez" duvarını içeriyordu
(`docs/domain/void-complimentary-discount-policy.md`, V0-DOM-006). Semih'in
onayıyla (2026-09-04) bu duvar, tek bir mutfak-zamanlama modelini herkese
dayattığı için gevşetildi: `docs/domain/void-complimentary-discount-policy.md`
`## Amendment` bölümü, gönderilmiş-ama-servis-edilmemiş bir kalemin artık
`bills.void` grant'iyle (politika motoru + delegasyon + yönetici — her
işletme kendi eşiğini ayarlar) void edilebileceğini kaydeder; bu yalnız bir
Ayarlar anahtarı (`kitchen.live_sync_enabled`, varsayılan kapalı) açıkken
ulaşılabilir bir yoldur.

Bu görev kararı kaydeder ve uygulama görev kümesini kayıt altına alır:

| Görev | Kapsam |
| --- | --- |
| `V1-SET-002` | Ayarlar modülüne `kitchen.live_sync_enabled` anahtarı (varsayılan kapalı) |
| `V1-KIT-005` | Mutfak bilet kalemi durum geçişlerinin Sipariş kalemine gerçekten yazılması (yalnız anahtar açıkken) |
| `V1-WTR-009` | Kalem "hazır" olduğunda siparişi alan garsonun telefonuna anlık bildirim (V1-KIT-005'e bağımlı) |
| `V1-ORD-005` | Gönderilmeden önce iptal — mevcut `ItemExceptionHandler.VoidItemAsync`'i gerçek bir endpoint'e bağlamak |
| `V1-BIL-005` | Servis edildikten sonra ücretsizleştirme — mevcut `ItemExceptionHandler.ApplyComplimentaryAsync`'i `bills.comp` grant'iyle bir endpoint'e bağlamak |
| `V1-IAM-027` | Gönderildi-ama-servis-edilmedi void: `OrderItem.Cancel()` sınırını genişletmek, mutfak biletini eşzamanlı düşürmek, açık bir Bill varsa `BillLineType.Waste` satırı yazmak, `bills.void` grant'iyle bağlamak (V1-KIT-005 + V1-ORD-005'e bağımlı) |

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-026-grant-class-bill-adjustment-surface.md`
- `plan/v1/settings/V1-SET-002-kitchen-live-sync-toggle.md`
- `plan/v1/kitchen-printing/V1-KIT-005-kitchen-order-item-state-sync.md`
- `plan/v1/waiter-pwa/V1-WTR-009-order-ready-notification.md`
- `plan/v1/orders/V1-ORD-005-pre-send-void-endpoint.md`
- `plan/v1/billing/V1-BIL-005-post-serve-comp-endpoint.md`
- `plan/v1/identity-authorization/V1-IAM-027-sent-unserved-void-with-waste.md`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır): `docs/domain/void-complimentary-discount-policy.md`
  (`V0-DOM-006` sahipliğinde kalır) — `## Amendment` bölümü eklendi, mevcut
  metin değişmedi.
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- Karar: void'in gönderildi-ama-servis-edilmedi durumu artık `bills.void`
  grant'iyle ulaşılabilir (yukarıdaki Amendment).
- Altı uygulama görevinin kaydı (yukarıdaki tablo), her biri kendi modülünün
  dizininde, kendi bağımlılık zinciriyle.

## Out of scope

- Uygulama kodu — altı çocuk görevin kapsamındadır.
- Fiscal sonrası iptal (refund yolu) — V0-DOM-003'ün kapsamındadır,
  değişmedi.

## Dependencies

- V1-IAM-025

## Acceptance evidence

- `docs/domain/void-complimentary-discount-policy.md` `## Amendment` bölümü
  eklendi, `markdownlint-cli2` temiz.
- Altı görev dosyası oluşturuldu; `python tools/plan-audit/plan_audit_tool.py
  validate` / `verify-manifest` sıfır hata.

## Handoff

- V1-SET-002
