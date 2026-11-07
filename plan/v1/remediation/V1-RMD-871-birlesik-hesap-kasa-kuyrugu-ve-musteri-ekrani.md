# V1-RMD-871 - Birleşik hesap kasa kuyruğunda ve müşteri ekranında tek hesap olarak görünür

- Task ID: V1-RMD-871
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Tanım: birleştirilmiş hesap (birden çok siparişten, bills.order_id boş) iki yerde yanlış görünüyor. (1) Kasa bekleyen hesap kuyruğu (GetChecksAwaitingPaymentAsync) siparişleri tek tek listeler: birleşik hesap aynı hesap kimliğiyle iki satır olur, her satır tüm hesabın ödenen tutarını kendi sipariş toplamıyla gösterir. (2) Müşteri ekranı tek bir sipariş gösterir; BindBillOrderToDisplayAsync siparişsiz (birleşik) hesabı hiç bağlamaz, müşteri ödenen hesabı göremez. Doğru davranış: kuyrukta birleşik hesap TEK satır (en eski sipariş temsilci; kalem sayısı, toplam ve önizleme hesabın tüm siparişlerinin birleşimi; ödenen tutar hesaba ait); müşteri ekranı birleşik hesabın en eski siparişine bağlanır ve anlık görüntü aynı açık hesabın bütün siparişlerinin kalemlerini ve toplamlarını gösterir. Sipariş kaydı olmayan (tezgah) hesaplar bağlanmamaya devam eder.

## Owned surface

- `evidence/V1-RMD-871/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/CashierHandoffStore.cs — birleşik hesabı kuyrukta tek satıra indirir.
  - src/Host/DualScreen/DualScreenStore.Display.cs — birleşik hesabı bağlar ve anlık görüntüde kardeş siparişleri toplar.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayBillBindingTests.cs — müşteri ekranı birleşik hesap testleri.
  - tests/Host/Experience/Billing/MergeOrdersHttpTests.cs — kuyruk testi tek satır beklentisine güncellenir.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: birleşik hesap kuyrukta tek satır (temsilci sipariş, toplamlar birleşik); iptalden sonra kalan sipariş tek başına kuyrukta.
2. Kırmızı test: birleşik hesap ekrana bağlanır, anlık görüntü iki siparişin kalem ve toplamlarını verir; siparişsiz tezgah hesabı bağlanmaz.
3. Mutasyon: gruplamanın kaldırılması, kalem/toplam toplamının kaldırılması, kardeş sipariş sorgusunun kaldırılması (cp+cmp geri alınır).

## Out of scope

- İstemci (PosTerminal) değişikliği; satır sayısı dışındaki kuyruk alanları.

## Dependencies

- None

## Acceptance evidence

1. Host Billing birleştirme testleri ve DualScreen bağlama testleri gerçek Postgres ile exit 0; çıktılar evidence/V1-RMD-871/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
