# V1-RMD-867 - Cari hesap bakiyesi yeniden kurulumu atomik; defterde aynı anahtar farklı tutarla gelirse sessizce eski satır dönmez

- Task ID: V1-RMD-867
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G10 (F0096, F0452): (1) PostgresAccountBalanceProjection.RebuildAsync atomik değildir: bütün defteri belleğe okur (limit int.MaxValue), sonra kilitsiz ve ayrı komutla mutlak değer yazar. Okuma ile yazma arasında defterde yeni bir satır olursa (bakiyeyi aynı işlemde artıran tetikleyici) o satır bakiyeden silinir; bakiye defterle ayrışır. Yeniden kurulum tek işlemde yapılır: önce müşterinin bakiye satırı kilitlenir (yoksa yer tutucu satırla), sonra toplam veritabanında tek SQL ile hesaplanıp yazılır; eşzamanlı defter yazımı kilidi bekler ve sonuca doğru eklenir. Bellek kullanımı da kalkar. (2) PostgresAccountTransactionLedger.RecordAsync aynı idempotency anahtarıyla farklı tutar ya da not gelirse (ON CONFLICT DO NOTHING) sessizce eski satırı döner; para kaydı yanlış sanılır. Aynı anahtarın farklı tutar ya da notla gelmesi AccountTransactionIdempotencyConflictException ile reddedilir (aynı gövde yeniden denenirse eski satır dönmesi aynen kalır). (3) F0097 (IBalanceSnapshotStore üretimde çağrılmıyor) doğrulandı: V14-ACC-002 planı bunu ileride yaşlanma raporuna temel diye bilinçli kurmuş; rapor ürün kararıdır, bu görevde değişmez, raporda listelenir.

## Owned surface

- `evidence/V1-RMD-867/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/CustomerAccounts/BalanceProjection/PostgresAccountBalanceProjection.cs — atomik yeniden kurulum.
  - src/Modules/CustomerAccounts/TransactionLedger/AccountTransactionIdempotencyConflictException.cs — yeni istisna.
  - src/Modules/CustomerAccounts/TransactionLedger/PostgresAccountTransactionLedger.cs — gövde karşılaştırma.
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresAccountBalanceProjectionTests.cs — eşzamanlı yazım ve yeniden kurulum testleri.
  - tests/Modules/CustomerAccounts/TransactionLedger/PostgresAccountTransactionLedgerTests.cs — çakışma testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. RebuildAsync: tek işlem, kilitli bakiye satırı, SQL toplamı; defter yazımı yeniden kurulum sırasında beklemeli ve sonuç doğru.
2. Defter: aynı anahtar ve aynı tutar/not tekrarı eski satırı döner; farklı tutar ya da not istisna.
3. Testler: eşzamanlı satır ekleme + yeniden kurulum sonunda bakiye = defter toplamı; boş defterde satır silinir; tutar ve not çakışması.
4. Mutasyon: kilit, SQL işaret kuralı (borç/alacak), çakışma karşılaştırması (tutar, not) (cp+cmp ile geri alınır).

## Out of scope

- Bakiye anlık görüntü deposunun (IBalanceSnapshotStore) bağlanması ya da silinmesi: ürün kararı.
- Yeniden kurulumu çağıran zamanlayıcı ya da uç nokta (bugün üretimde çağıran yok; araç olarak kalır).

## Dependencies

- None

## Acceptance evidence

1. CustomerAccounts testleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-867/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
