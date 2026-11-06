# V1-RMD-864 - Ödeme uzlaşma taraması: zamanlayıcı, kalıcı satırları yeniden okumama, ham hata metni ve zayıf eşik testi

- Task ID: V1-RMD-864
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G10 (F0092, F0093, F0447, F0448): (1) Ödeme farkı taraması (PaymentReconciliationScanner) yalnız elle POST /reconciliation-scan ile çalışır; çevrimiçi sipariş taramasının aksine zamanlayıcısı yoktur, bu yüzden "periyodik kontrol" iddiası boş kalır ve bekleyen Unknown ya da onaylı-ama-tahsisatsız ödeme kimse tuşa basmazsa vaka olmaz. Mevcut zamanlayıcı hizmeti ödeme taramasını da çalıştıracak biçimde genelleştirilir (adı ReconciliationScanHostedService olur); iki tarama ayrı ayrı denenir, birinin hatası diğerini durdurmaz. (2) CashDifference ve AllocationProviderMismatch kaynakları, kapanmış oturum farkı ve onaylı deneme tutarı bir daha değişmediği halde her taramada tüm geçmişi okur; vakası zaten bulunan satırlar sorguda elenir (vaka anahtarı her durumda tekrar vaka açmadığı için sonuç aynıdır) ve 5000 satır sınırı artık yalnız vakası olmayan satırları sayar. (3) Tarayıcı bir kaynak hatasında ham ex.Message'ı yönetim yanıtına döner; çevrimiçi tarayıcıdaki gibi sabit, ayrıntısız bir neden döner. (4) Testte ">= 1" zayıf iddia ve 100 TL şiddet eşiği sınanmamış; kesin iddia ve eşik testi eklenir.

## Owned surface

- `evidence/V1-RMD-864/**`
- `src/Host/Experience/Reconciliation/ReconciliationScanHostedService.cs` — eski OnlineOrderReconciliationHostedService yerine yeni ad.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reconciliation/Payments/PaymentReconciliationScanner.cs — sabit hata nedeni.
  - src/Modules/Reconciliation/Payments/CashSessionDifferenceSourcePair.cs — vakası olan satırı ele.
  - src/Modules/Reconciliation/Payments/CardSettlementAllocationMismatchSourcePair.cs — vakası olan satırı ele.
  - src/Host/Experience/Reconciliation/OnlineOrderReconciliationHostedService.cs — ReconciliationScanHostedService olarak yeniden adlandırılır, ödeme taramasını da çalıştırır.
  - src/Host/Experience/Reconciliation/OnlineOrderReconciliationEndpoints.cs — kayıt.
  - tests/Host/Experience/Reconciliation/OnlineOrderReconciliationHostedServiceTests.cs — eski ad.
  - tests/Host/Experience/Reconciliation/ReconciliationScanHostedServiceTests.cs — yeni ad ve ödeme taraması testi (yeni dosya).
  - tests/Modules/Reconciliation/Payments/PaymentReconciliationScannerTests.cs — kesin iddia, eşik, elenen satır, sabit neden.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Zamanlayıcı ödeme taramasını da çalıştırır; bir tarama hatası diğerini ve sonraki turu durdurmaz.
2. Vakası olan kalıcı satır sorguda elenir; vakasız satır hâlâ vaka olur.
3. Kaynak hatasında yanıt sabit neden taşır (ham metin yok).
4. Testler: zamanlayıcı ödeme farkını vakaya çevirir; elenen satır; 100 TL eşiği; sabit neden; kesin sayı iddiası.
5. Mutasyon: eleme koşulu, eşik, sabit neden, zamanlayıcıda ödeme taraması (cp+cmp ile geri alınır).

## Out of scope

- Tekrar görülen vakanın "Deduplicated" eylem satırının her turda eklenmesi (çevrimiçi tarama da aynı çalışır): geçmiş satırı sıklığı ürün kararıdır, rapora yazılır.
- Hugin/Token terminal toplamları ve diğer kapalı kaynaklar.

## Dependencies

- None

## Acceptance evidence

1. Reconciliation.Payments ve Host Reconciliation testleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-864/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
