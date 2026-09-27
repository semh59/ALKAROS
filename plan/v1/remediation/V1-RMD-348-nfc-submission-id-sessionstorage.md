# V1-RMD-348 - NFC sipariş kimliği artık sayfa yenilenirse hafızadan silinmiyor

- Task ID: V1-RMD-348
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "NFC'de sayfa yenilenirse/arka plana
düşerse kopya sipariş riski (`NfcOrder.tsx:31-35`, `submissionIdRef` yalnız bellekte; QR tarafı `sessionStorage`
ile korunuyor, NFC korunmuyor)." Doğrulandı: `submissionIdRef` salt bir React `useRef` idi — bir gönderim
sürerken sayfa yenilenirse (veya arka plana düşüp tarayıcı sekmeyi kapatırsa) bu değer tamamen kaybolurdu. Bir
sonraki denemede `??=` YENİ bir `crypto.randomUUID()` üretirdi — sunucunun `ux_orders_table_submission`
idempotency indexinin KORUYAMAYACAĞI, GERÇEK bir çifte sipariş riski (iki farklı idempotency key = iki farklı
sipariş). QR'ın kendi eşdeğeri (`order-entry.js`'nin `readOrCreateSubmissionId()`) tam olarak bu nedenle
`sessionStorage` kullanıyordu; NFC aynı korumaya sahip değildi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx
- `plan/v1/remediation/V1-RMD-348-nfc-submission-id-sessionstorage.md`

## In scope

1. `submissionIdRef`, artık `sessionStorage`'a `alkaros.nfc.submissionId.{tableId}` anahtarıyla yazılıyor/
   okunuyor (masaya göre kapsamlandırıldı — aynı telefonda farklı masalara ait iki NFC sekmesi asla aynı
   anahtarı paylaşmasın diye). Bileşen ilk render'da bu anahtarı okuyor; böylece bir yenileme SONRASI ilk
   render, önceki denemenin kimliğini geri kazanıyor.
2. `sessionStorage` erişilemez olabileceği (gizli sekme) durumlar için `try/catch` ile korunuyor — bellekteki
   değer yine de bu oturumun kendi hayatı boyunca korunuyor (QR'ın kendi deseniyle aynı savunmacı yaklaşım).

## Out of scope

1. Arka plana düşme (uygulama arka planda askıya alınma) davranışının ayrıca test edilmesi — tarayıcı bunu
   React state'ini kaybetmeden yönetir (sayfa gerçekten yeniden yüklenmediği sürece); gerçek risk yalnızca
   GERÇEK bir sayfa yeniden yüklemesi/kapanmasında.

## Dependencies

- None

## Acceptance evidence

- `npx vitest run src/routes/NfcOrder.test.tsx`: 6/6 test geçti (2 yeni test dahil):
  - "picks up a submissionId a previous (reloaded-away) mount already persisted, instead of generating a new
    one" — `sessionStorage`'a önceden bir kimlik yazılıp bileşen yeniden monte edildiğinde, gönderilen kimliğin
    YENİ üretilen değil, ÖNCEDEN VAR OLAN kimlik olduğu doğrulandı; başarılı gönderim sonrası anahtarın
    temizlendiği de doğrulandı.
  - "two browser tabs open on different tables never collide on the same submissionId key" — masaya göre
    kapsamlama doğrulandı.
- Mutasyon kontrolü: `NfcOrder.tsx` eski (yalnız-bellekte) haline döndürüldü (`git stash`), yeni test GERÇEK bir
  çalışma zamanı farkıyla kırmızıya döndü (`Expected: "existing-in-flight-id", Received: "<yeni rastgele
  UUID>"` — tam olarak denetimin tarif ettiği çifte-kimlik riski). Dosya geri yüklendi (`diff` ile bayt-bayt
  doğrulandı), paket yeniden 6/6 yeşile döndü.
- `npx tsc --noEmit`: sıfır hata. `npx vitest run` (PosTerminal'in tüm paketi): 31 dosya, 238 test, tümü yeşil
  (regresyon yok).

## Handoff

- None
