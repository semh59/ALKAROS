# V1-RMD-314 - Kasa tahsilat/nakit/indirim/bahşiş formları her denemede yeni idempotency anahtarı üretiyordu

- Task ID: V1-RMD-314
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K3 bulgusu: `split-payment.js`'in `submitTender`/`submitDiscount`/`submitTip` fonksiyonları ve `cash-session.js`'in `submitCashMovement` fonksiyonu, gönderdikleri `IdempotencyKey`'i her çağrıda `crypto.randomUUID()` ile taze üretiyordu. Sunucu tarafı idempotency koruması (V1-RMD-241, V1-RMD-258 vb.) gerçek ve doğru çalışıyor, ama istemci hiçbir zaman AYNI anahtarı iki kez göndermiyordu — bir ağ zaman aşımı sonrası "tekrar dene" (aynı düğmeye yeniden tıklama) sunucu tarafında tamamen YENİ bir tahsilat/nakit hareketi/indirim/bahşiş denemesi olarak işleniyordu. İstek gerçekten sunucuya ulaşıp işlendiyse ama yanıt istemciye ulaşmadıysa (gerçek dünyadaki en yaygın "zaman aşımı" şekli), bu mükerrer bir para kaydı yaratıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-314-tender-idempotency-key-stable-across-retry.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/09-split-payment-race-and-edges.spec.js

## In scope

1. `submitTender`, `submitDiscount`, `submitTip` (split-payment.js) ve `submitCashMovement` (cash-session.js): anahtar bir kez üretilip geçerli deneme için saklanır; yalnız gerçek bir sunucu yanıtı (başarı VEYA kesin bir ret) geldiğinde temizlenir, bir ağ hatasında (catch) korunur.
2. Gerçekten farklı bir deneme başladığında (yöntem/tutar değişimi, yeni sayfa yüklemesi, eşit bölüşüm yeniden hesaplaması) anahtar açıkça sıfırlanır.

## Out of scope

- Sunucu tarafı idempotency mantığı (zaten doğru, V1-RMD-241/V1-RMD-258).
- PosTerminal (React) tarafındaki eşdeğer akışlar — bu görevin Owned surface'ı yalnız vanilla Cashier JS.

## Dependencies

- None

## Acceptance evidence

Yeni bir E2E testi (`09-split-payment-race-and-edges.spec.js`, gerçek Chromium + gerçek `ALKAROS.Host.dll` + gerçek Postgres) ağ isteğini `page.route`'la tamamen bloke edip (`route.abort('failed')`) gerçek "tekrar dene" akışını (aynı düğmeye ikinci kez tıklama) tetikliyor ve iki denemede de sunucuya gönderilen `IdempotencyKey`'in AYNI olduğunu doğruluyor (`new Set(capturedKeys).size === 1`). Cashier E2E paketi tamamı çalıştırıldı: 20/20 ilgili spec (05, 08, 09, 10, 18) yeşil, tam paket 41 testten 40/41 (kalan 1 hata `V1-RMD-298`'de belgelenen, bu göreve dahil olmayan önceden var olan masa-devri bulgusu).

Mutasyon kontrolü: `submitTender`'ın anahtar-yeniden-kullanım satırı geçici olarak eski hâline (`crypto.randomUUID()` doğrudan) döndürüldü — yeni test gerçekten kırmızı oldu (`new Set(capturedKeys).size` 1 değil 2 döndü); dosya `diff` ile birebir orijinaline geri getirildi, tüm ilgili spec'ler tekrar yeşil.

## Handoff

- None
