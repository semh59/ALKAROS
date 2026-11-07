# V1-RMD-869 - Birleştirme akışında kilit beklerken silinen sipariş dalına yarış testi

- Task ID: V1-RMD-869
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

BillingSplitStore.LockAndCheckOrdersAsync kilit altında okunan satır sayısı istenen sipariş sayısından azsa 'sipariş kayboldu' hatası (BillingSplitNotFoundException, 404 NOT_FOUND) verir. Bu dal hiçbir testle sınanmıyor: kilit beklerken bir sipariş silinirse birleştirmenin hesap açmadığı ve 404 döndüğü kanıtsız. Mevcut iptal ve masa değişimi yarış testlerinin kalıbıyla (kilit tutan işlem, pg_blocking_pids ile bekleme doğrulaması) silinme senaryosu eklenir.

## Owned surface

- `evidence/V1-RMD-869/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/MergeOrdersHttpTests.cs — silinen sipariş yarış testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Test: kilit tutan işlem siparişi ve kalemlerini siler, birleştirme kilitte bekler, işlem onaylanınca yanıt 404 NOT_FOUND olur, hesap ve hesap kalemi oluşmaz.
2. Mutasyon: satır sayısı kontrolünün kaldırılması testi düşürür (cp+cmp geri alınır).

## Out of scope

- Üretim kodu değişikliği (davranış doğruysa).

## Dependencies

- None

## Acceptance evidence

1. Host Billing birleştirme testleri gerçek Postgres ile exit 0; çıktılar evidence/V1-RMD-869/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
