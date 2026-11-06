# V1-RMD-842 - Uygulanmış ikramın tekrar gönderiminde kalem kontrolü 409 döndürüyor

- Task ID: V1-RMD-842
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-841 incelemesinde bulunan kardeş kusur: `POST .../items/{itemId}/comp` ucu, `bills.comp` yetkisi olmayan kullanıcı için yetki talebinden önce kalem değiştirilebilirlik kontrolünü (V1-RMD-836) çalıştırıyor. Onaylanıp uygulanmış bir ikram aynı idempotency anahtarıyla tekrar gönderildiğinde, arada sipariş veya hesap kapanmışsa kontrol 409 (`ORDER_CLOSED`, `BILL_NOT_MODIFIABLE`) döndürüyor; oysa aynı anahtar ilk sonucu yeniden vermeli.

Düzeltme: aynı anahtara kayıtlı bir yetki varsa ön kontrol atlanır (V1-RMD-841 `void-sent` çözümüyle aynı desen); yeni anahtarda kontrol yetki talebinden önce çalışmaya devam eder. Hata kodları değişmez.

## Owned surface

- `evidence/V1-RMD-842/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-BIL-005) — ön kontrol koşulu.
  - tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs (V1-WTR-012) — yeni test.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test (gerçek Postgres): garson ikram ister, yönetici onaylar, ikram uygulanır; sipariş tamamlanır; aynı anahtarla tekrar gönderim 200 ve ilk sonucu döndürür (409 değil).
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır).
3. İngilizce kod ve test adları.

## Out of scope

- Ucun diğer davranışları, `void-sent` ucu (V1-RMD-841'de bitti).

## Dependencies

- V1-RMD-841

## Acceptance evidence

1. Comp HTTP test projesi tam yeşil; kırmızı ve yeşil çıktı `evidence/V1-RMD-842/` altında.
2. Mutasyon: koşul kaldırılınca (her tekrar gönderimde ön kontrol) yeni test kırılır; yeni anahtarda ön kontrol kaldırılınca 836 testleri kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
