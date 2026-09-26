# V1-RMD-340 - Garson PIN tuşuna çift dokunuş artık tek bir sunucu isteğine indirgeniyor

- Task ID: V1-RMD-340
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Garson PIN ekranında çifte dokunuşa karşı
koruma yok (`kiosk-lock.js:173-211`); ücretsiz iptalde (`confirmVoid`) idempotency key eksik, kardeş akışlarla
tutarsız." İki ayrı iddia araştırıldı:

1. **PIN çift dokunuşu (doğrulandı, gerçek bulgu):** `submitPin()`'in hiçbir "işlem sürüyor" koruması yoktu —
   PIN tuş takımının '✓' tuşuna hızlı çift dokunuş, ilk istek hâlâ beklerken ikinci bir eşzamanlı
   `POST /api/v1/auth/unlock` isteği tetikliyordu. Yanlış bir PIN için bu, sunucu tarafında İKİ başarısız deneme
   sayacı yakıyordu — hesabın sunucunun kendi politikasının öngördüğünden yaklaşık iki katı hızlı kilitlenmesine
   yol açıyordu.
2. **`confirmVoid` idempotency key eksikliği (araştırıldı, gerçek ama farklı bir risk sınıfı — kapsam dışına
   ayrıca not edildi):** Bkz. "Out of scope".

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/kiosk-lock.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/07-kiosk-lock.spec.js
- `plan/v1/remediation/V1-RMD-340-pin-pad-double-tap-guard.md`

## In scope

1. `submitPin()`: yeni bir modül-seviyesi `pinSubmitInFlight` bayrağı — istek sürerken gelen HERHANGİ bir çağrı
   sessizce yok sayılıyor (kullanıcıya hiçbir yanlış "hata" göstermeden), bayrak `finally` içinde temizleniyor.
2. Gerçek bir Chromium E2E testi (`07-kiosk-lock.spec.js`): `/auth/unlock` isteğini kasıtlı olarak 300ms
   geciktiren bir `page.route` ile, GERÇEK bir çift dokunuşun (birinci istek havada asılıyken ikinci tıklama)
   sunucuya tam olarak BİR istek ulaştığını doğruluyor.

## Out of scope

1. **`confirmVoid`'e (ücretsiz iptal) idempotency key eklemek.** Araştırma: `void-sent`/`comp` akışlarının kendi
   idempotency key'leri, o akışlara ÖZGÜ bir nedenle var — yönetici onayına giden 202 Pending yanıtı, bir
   yeniden deneme aynı isteğe çözülsün diye anahtarı kalıcı tutuyor (bkz. `openVoidSentSheet`'in kendi yorumu).
   Ücretsiz iptalin (`confirmVoid`) böyle bir bekleyen-onay adımı yok — tamamen senkron. Sunucu tarafında zaten
   gerçek bir koruma var: `VoidItemAsync`'in `expectedRowVersion` optimistik eşzamanlılık kontrolü, aynı isteğin
   GERÇEKTEN iki kez başarıyla uygulanmasını (çifte iptal) matematiksel olarak imkânsız kılıyor — ikinci deneme
   `StaleOrderRowVersionException` (409) ile başarısız olur. Gerçek risk çifte iptal DEĞİL: eğer birinci istek
   sunucuda başarıyla uygulanır ama yanıt ağ kesintisiyle kaybolursa, garson tekrar dener ve KAFA KARIŞTIRICI bir
   409 görür (işlem zaten gerçekleşmiş olsa da). Bunu doğru çözmek — sunucu tarafında gerçek bir idempotency-key
   deduplikasyon tablosu (bu oturumun `SubmitOrderHandler`'daki `idempotency_keys` deseni gibi) — bu bulgunun
   kapsamından çok daha büyük, ayrı bir görev; PIN çift-dokunuş korumasıyla aynı görevde acele bir yamayla
   kapatılırsa test edilmemiş, riskli bir değişiklik olurdu.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/WaiterPwa` (specs/07-kiosk-lock.spec.js): 14/14 test geçti (1 yeni test dahil:
  "PIN tuşuna çift dokunuş sunucuya tek bir kilit açma isteği gönderir (V1-RMD-340)") — gerçek Chromium + gerçek
  Host + gerçek Postgres'e karşı.
- Mutasyon kontrolü: `pinSubmitInFlight` koruması geçici olarak kaldırıldı (`git stash`), yeni test GERÇEK bir
  çalışma zamanı farkıyla kırmızıya döndü (`Expected: 1, Received: 2` — sunucuya iki ayrı istek ulaştığı
  doğrulandı). `git stash pop` ile geri getirildi (`diff` ile bayt-bayt doğrulandı), paket yeniden 14/14 yeşile
  döndü.
- `dotnet build ALKAROS.slnx -c Debug`: sıfır hata (bu oturumun K10/M1-M8 değişiklikleri dahil tüm çözüm).

## Handoff

- None
