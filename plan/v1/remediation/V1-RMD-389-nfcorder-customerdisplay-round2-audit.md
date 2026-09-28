# V1-RMD-389 - PosTerminal NfcOrder + CustomerDisplay Tur 2 denetimi: sepet kalıcı değildi

- Task ID: V1-RMD-389
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 14: PosTerminal'in
`NfcOrder.tsx` (NFC ile açılan, girişsiz masa siparişi ekranı) ve `CustomerDisplay.tsx` (müşteri
ekranı). `CustomerDisplay.tsx` zaten çok olgun (V1-CDP tazelik protokolü, SignalR + poll ikilisi,
bağlantı kaybı bildirimi) — Tur 2'de ek bir bulgu çıkmadı.

`NfcOrder.tsx` gerçek bir müşterinin KENDİ telefonunda, girişsiz açtığı bir sayfa — bir personel
terminalinden çok daha sık kesintiye uğrar (telefon araması, ekran kilidi, işletim sisteminin sekmeyi
bellekten atması). Dosyanın kendi `submissionIdRef`'i (V1-RMD-348) tam olarak bu senaryo için zaten
sessionStorage'a yazılıyordu — ama SEPETİN kendisi yalnızca React state'inde yaşıyordu. Müşteri henüz
"Siparişi Gönder"e basmadan önce bu kesintilerden biri yaşanırsa, seçtiği her şey sessizce siliniyor,
hiçbir uyarı ya da geri dönüş yolu olmadan.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-389-nfcorder-customerdisplay-round2-audit.md`

## In scope

1. **[P2, Orta-Yüksek — saha gerçekliği] Sepet, sayfa yeniden yüklendiğinde/arka plana
   alındığında kayboluyordu.** `submissionIdRef`'in kendi masaya-özel sessionStorage anahtarıyla
   (`alkaros.nfc.submissionId.{tableId}`) aynı desende, `alkaros.nfc.cart.{tableId}` anahtarı
   eklendi. Cart state'i artık mount sırasında bu anahtardan geri okunuyor, her değişimde
   yazılıyor (boşaldığında anahtar tamamen siliniyor) ve sipariş başarıyla gönderildiğinde
   temizleniyor — böylece "placed" ekranından sonra bir yeniden yükleme, zaten tamamlanmış eski
   sepeti geri getirmiyor. `sessionStorage` erişilemez olduğu durumlar (gizli sekme) için aynı
   try/catch fallback deseni kullanıldı.

## Out of scope

- `CustomerDisplay.tsx`: gerçek bir ek bulgu yok — zaten en olgun ekranlardan biri.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (40 dosya,
  299 test, bu görevin 2 yeni testi dahil): 299/299 geçti, regresyon yok.
- Mutation-check: yalnızca `NfcOrder.tsx` `git stash` ile geri alındı — iki yeni test GERÇEKTEN
  kırmızı oldu (sepet geri yüklenmedi; sipariş sonrası anahtar temizlenmedi). `git stash pop` ile
  geri yüklendi, tam paket tekrar 299/299 yeşile döndü.

## Handoff

- None
