# V1-RMD-739 - Bitmiş görev doğrulama programı (1276 Done görev ve tüm md dosyaları)

- Task ID: V1-RMD-739
- Status: InProgress
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-27

## Goal

Planda Done görünen bütün görevlerin gerçekten hatasız bittiğini görev görev kanıtlamak: sahte veya varsayımsal kod, ölü kod, teknik borç ve kötü algoritma bırakılmaz. Aynı programda proje başlangıcından beri bütün md dosyaları (plan, docs ve diğerleri) kodun bugünkü haline göre güncel hale getirilir. Bu program bitmeden yeni özellik, yeni ekran veya yeni denetim görevi başlamaz. Programın pilotu V1-RMD-604'tür (Payments).

## Owned surface

- `plan/v1/remediation/V1-RMD-739-bitmis-gorev-dogrulama-programi.md`
- `evidence/V1-RMD-739/**`
- `tools/verification/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

Plan ile kod nasıl kıyaslanır (programın çekirdeği): her görev md dosyasındaki iddia üç kaynakla çapraz kontrol edilir. (1) Plan iddiası: Goal, In scope ve Acceptance maddeleri. (2) Kod gerçeği: görevin Owned surface dosyaları bugünkü HEAD'de okunur; ilgili uç noktalar, tablolar, ekranlar ve testler adıyla aranır; çalıştırılabilir kanıt (test, sorgu, mutasyon) gerekiyorsa koşulur. (3) Geçmiş kanıtı: kapanış commit'leri ve `evidence/<görev>/` günlükleri. Her kabul maddesi için sonuç şu tabloya düşer: madde, kodda nerede, hangi test, hangi kanıt, sonuç (KARŞILANIYOR, KISMEN, KARŞILANMIYOR, KANITSIZ). Plan koddan farklıysa hangisinin doğru olduğu koda ve çalıştırmaya bakılarak belirlenir: kod yanlışsa düzeltme görevi, plan bayatsa md güncellenir, ikisi de eksikse ikisi de açılır.

Aşama 0 - Defter (otomatik): Her Done görev için bir satır: görev no, modül, sahip yüzey dosyaları, kabul maddeleri, kapanış commit'leri, kanıt klasörü, kanıt günlüklerindeki gerçek çıkış kodları. Eksik kanıt, sıfır olmayan çıkış kodu ve boş kabul maddesi işaretlenir. Çıktı `evidence/V1-RMD-739/ledger.jsonl`.

Aşama 1 - Tüm depo taramaları (otomatik, deterministik; yanlış pozitifler elle elenir):

1. Sahte ve varsayımsal kod: `NotImplementedException`, `TODO/FIXME/HACK/STUB/FAKE/SIMULATED`, sabit örnek veri, üretim yolunda `InMemory`, `Fake`, `Stub` sınıfı, kapalı bayrakla gizlenen gerçek iş.
2. Ölü kod: kullanılmayan public ve internal üyeler (Roslyn IDE0051, IDE0052, CA1812 ve çözüm geneli referans taraması), hiçbir uçtan, arayüzden veya hosted servisten ulaşılmayan sınıflar; ulaşılabilirlik kapısı izin listesindeki her kayıt gerekçelendirilir ya da silinir.
3. Teknik borç: yutulan istisna, `#pragma warning disable`, bastırılmış analizör, atlanan test, yorum satırına alınmış kod.
4. Algoritma: sınırsız sorgu, N+1, döngü içinde veritabanı çağrısı, O(n²) koleksiyon taraması, para yollarında kilitsiz oku-değiştir-yaz, eksik idempotency, kuruş yuvarlama.
5. Veritabanı: her migration için down, yabancı anahtar dizinleri, NOT VALID bırakılan kısıtlar, yazılan ama hiç okunmayan kolonlar.
6. İstemci: İngilizce sızıntı, ham hata metni, yükleniyor/boş/hata durumu eksikliği, 48 px altı dokunma hedefi, zaman aşımsız fetch.
7. Test gücü: modül başına örnekleme mutasyonu; para ve yetki yollarında görev başına en az 3 mutant.
8. Belge ve plan güncelliği: her md dosyasındaki iddia (sayılar, durumlar, dosya yolları, uç noktalar, sınır kuralları, bağımlılık grafı) koda ve plan durumuna karşı kontrol edilir; bayat olan düzeltilir, kanıtı olmayan iddia silinir veya işaretlenir. Proje başlangıcından beri bütün md dosyaları kapsamdadır.

Aşama 2 - Görev görev doğrulama. Sıra risk sırasıyla: (1) Payments, Cash, Billing, CustomerAccounts, Invoicing; (2) Identity, Security, Privacy, Audit; (3) Orders, Tables, Kitchen, OnlineOrdering, QrOrdering; (4) Inventory, Purchasing, Recipes, Production, Catalog, Menu; (5) Reporting, Reconciliation, Settings, Support, Operations, Observability, CustomerData; (6) Cashier, PosTerminal, WaiterPwa; (7) V0 temelleri ve çapraz görevler. Her görev için görev kartı (10 görevlik gruplar halinde ajanlara verilir):

- A. Kabul: her kabul maddesi bir koda, bir teste ve bir kanıta bağlanır; bağlanamayan madde bulgudur.
- B. Gerçeklik: sahip yüzey dosyaları bugünkü HEAD'de okunur; sahte, ölü, borçlu veya simüle kod aranır; dışarıya bağlı kısım açıkça Engelli işaretli olmalıdır.
- C. Algoritma: karmaşıklık, sınırlar, işlem, kilit, idempotency, hata ve iptal yolları satır satır okunur.
- D. Test: testler davranışı mı doğruluyor; mutasyon örneği çalıştırılır.
- E. Arayüz (varsa): Türkçe, durumlar, hata eşleme, erişilebilirlik.
- F. Kanıt: günlükler gerçek mi, çıkış kodları sıfır mı, commit'te yazılı sayılarla uyuşuyor mu.

Karar: TAMAM, DÜZELTİLMELİ (bulgu listesi ve ciddiyet) veya YENİDEN AÇ. Çıktı: `evidence/V1-RMD-739/tasks/<modül>.md` ve defterde karar. Bağımsız kontrol: her modülün kararlarının %15'i `qa-bulgu-dogrulayici` ile yeniden doğrulanır; çıkan her fark modülün yeniden taranmasını başlatır.

Aşama 3 - Düzeltme: her Orta ve üstü bulgu için düzeltme görevi (kırmızı test, düzeltme, mutasyon kanıtı); düşük bulgular modül kapanışında toplu düzeltilir.

Aşama 4 - Kapanış koşusu: Aşama 1 taramaları sıfır bulgu (gerekçeli istisnalar hariç), bütün test grupları yeşil, `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı, defterde karar eksiği sıfır.

Aşama 5 - Kalan işlerin belgelenmesi: açık görevler ve doğrulamada çıkan "yapılmadı" veya "dış bağımlı" kalemler için her biri ayrı md: amaç, koddan doğrulanmış mevcut durum, tam yapılacak iş listesi, kabul maddeleri, bağımlılık, risk, PO kararı gereken noktalar, iş büyüklüğü; ayrıca dizin dosyası. Yeni iş yalnız bu aşama bitince başlar.

Maliyet: Aşama 0 ve 1 ucuz ve kısa. Aşama 2 yaklaşık 130 ajan grubu ve %15 bağımsız kontrol; kesin rakam pilottan (V1-RMD-604, Payments) ölçülür ve Semih'e raporlanır. Oturum dolarsa durum `evidence/V1-RMD-739/progress.md` ve defterden sürdürülür.

## Out of scope

- Doğrulama sırasında bulunan düzeltmeler dışında yeni özellik ve yeni ekran.
- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Defterde 1276 görevin tamamı için karar vardır (TAMAM, DÜZELTİLDİ veya AÇIK-DIŞ); kararsız görev sıfırdır.
2. Aşama 1 taramalarının hepsi sıfır bulgudur ya da her istisna gerekçeli ve belgelidir.
3. Her Orta ve üstü bulgu kapanmış düzeltme görevine bağlıdır; mutasyon kanıtı vardır.
4. Her modülün kararlarının %15'i bağımsız ajanla yeniden doğrulanmış ve fark sıfırdır.
5. Bütün md dosyaları (plan, docs ve diğerleri) kodun bugünkü haliyle uyumludur; bayat iddia sıfırdır.
6. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.
7. Kalan her iş için ayrıntılı md belgesi ve dizin dosyası vardır.

## Handoff

- V1-RMD-605
