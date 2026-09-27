# V1-RMD-374 - PosTerminal NfcOrder + CustomerDisplay modül denetimi: axe taraması + eksik test dosyası

- Task ID: V1-RMD-374
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 14. modülü:
PosTerminal'in iki misafir/müşteri-yüzeyi ekranı — `NfcOrder.tsx` (267 satır, NFC etiketiyle
açılan self-servis sipariş sayfası) ve `CustomerDisplay.tsx` (385 satır, kasanın eşleştirilmiş
ikinci ekranı). On iki boyut üzerinden tarandı.

Her iki dosya da zaten olgun: `NfcOrder.tsx` bir sayfa yenilemeden sağ kalan, masa-kapsamlı bir
idempotency-key'i `sessionStorage`'da tutuyor (V1-RMD-348, mükerrer sipariş koruması);
`CustomerDisplay.tsx` bağlantı kopması durumunda eski tutarları güvenlik nedeniyle ekrandan
kaldırıyor ("Eski ürün ve tutarlar güvenlik nedeniyle ekrandan kaldırıldı"), `role="alert"`/`"status"`
ayrımını doğru çiziyor, eşleştirme kodu + boşta-ekran (screensaver) + tamamlanan-sipariş
gizleme akışlarının hepsi zaten sağlam. Hiçbiri kendi özel hata sınıfını fırlatmıyor (paylaşılan
`ApiError` kullanıyorlar) — Modül 7/8/9/12'nin sistemik bulgusu burada yok.

İki test-kapsama bulgusu:

1. **[Modül 5/10/12/13 sınıfı] `NfcOrder.tsx`'in axe-core taraması yoktu.**
2. **`CustomerDisplay.tsx`'in — kasanın ikinci ekranında GERÇEKTEN müşterinin gördüğü tek
   sayfa — hiçbir test dosyası yoktu.**

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx
- `src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-374-posterminal-nfc-customerdisplay-ui-audit.md`

## In scope

1. **[T1, Yüksek — test kapsamı] `NfcOrder.tsx`'in axe-core taraması yoktu.** Standart desenle
   eklendi.
2. **[T1, Yüksek — test kapsamı] `CustomerDisplay.tsx`'in hiç test dosyası yoktu.** Yeni dosya:
   eşleştirme kodu ekranı, aktif sipariş satırları/toplamı (authoritative snapshot'tan), 7
   saniye sonra tamamlanan siparişin gizlenip karşılama kartına dönmesi (V1-CDP zaman aşımı
   davranışı) ve axe taraması kapsandı.

## Out of scope

- Üretim kodu değişmedi — hiçbir ekranda gerçek bir davranış hatası bulunmadı.
- Ürün-katmanı (P1-P4) gözlemi yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (39
  dosya, 293 test, bu görevin 5 yeni testi dahil): 293/293 geçti, regresyon yok.
- Üretim kodu değişmediği için mutation-check gerekmedi (Modül 10/12/13'ün axe/test-only
  kapanışlarında olduğu gibi).
- Yeni `CustomerDisplay.test.tsx` yazımında iki gerçek düzeltme yapıldı (kendi hatalarım):
  (1) `/pairings` yerine gerçek uç nokta `/pairing-requests` kullanıldı; (2) test verisindeki
  `expiresAt` çok uzak bir tarih (2099) olduğunda `setTimeout`'un 32-bit sınırını aşan bir
  gecikme değeri oluşuyordu (zararsız bir uyarı, test başarısını etkilemiyordu) — gerçekçi,
  yakın bir gelecek tarihine değiştirildi.

## Handoff

- None
