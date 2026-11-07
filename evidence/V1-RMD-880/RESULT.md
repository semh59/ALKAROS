# V1-RMD-880 - Cari yaşlandırma raporu ve bakiye anlık görüntülerinin gerçek akışa bağlanması (F0097)

## Sonuç
`IBalanceSnapshotStore` artık üretimde çağrılıyor ve okunuyor; silinen kod yok, eksikler tamamlandı.
- Yazma: `balance-snapshot` bakım işi (6 saatte bir; Güvenlik/Sistem ekranından elle de çalışır). Her biten işletme gününün kapanış bakiyesini defterden hesaplar (canlı bakiyeden değil: iş saati ne olursa olsun aynı sonuç). Son 100 günde eksik günleri tamamlar; defter değişmez olduğu için ilk çalıştırma 90 günlük geçmişi de doğru doldurur.
- Okuma ve rapor: `GET /api/v1/management/reports/customer-aging` (yönetici/süpervizör oturumu + `reports.view`, kendi hata alanı), Yönetim ekranı "Cari yaşlandırma". 0-30 / 31-60 / 61-90 / 90+ gün kovaları defterden FIFO ile (ödemeler en eski borcu kapatır; kredi politikasındaki vadesi geçmiş kuralıyla aynı), fazla ödeme ayrı sütun, 30/60/90 gün önceki bakiye anlık görüntüden, `check.isBalanced` = kova toplamı - fazla ödeme = önbellek bakiyesi.
- Silinen/değişen üyeler: `TakeSnapshotAsync` (çağıranın verdiği bakiyeyi yazıyordu), `GetSnapshotAsync`, `GetSnapshotsAsync` (en eski 1000 satırı döndürüyordu) kaldırıldı; yerine `TakeClosingSnapshotsAsync`, `GetSnapshotsOnAsync` (tek sorgu), `GetSnapshotDatesAsync`. `unreachable_services_allowlist.json` içindeki iki girdi çıkarıldı.
- `IAccountBalanceProjection.RebuildAsync`: üretimde çağıran yok, V0-DAT-004 "tam yeniden kurulum" kuralı için bırakıldı (kapsam dışı, karar gerekir).

## Kanıt
- Gerçek Postgres: BalanceProjection 31+, Host CustomerAccounts 27 (CustomerAging testleri dahil), Host.Errors 230, SecurityAdministration 44; vitest 5, tsc ve oxlint temiz; plan-audit/consistency-audit kapanışta.
- Mutasyon (mutations.txt): 22 mutant; ilk turda M5 (aging SQL Adjustment işareti), H5 (60/90 sütun yer değişimi), H6 (check hep true) yaşadı; testler güçlendirilince M5b/H5b/H6b öldü. Diğerleri ilk turda öldü (kova sınırları, FIFO sırası, snapshot kesimi, 06:00, fazla ödeme, kayıt satırı, izin kontrolü, ekran yetkisi).

## Kararlar (ürün/mevzuat sorusu sorulmadı)
- Yaş işlem tarihinden (vade tarihinden değil); kova sınırları 30/60/90 dahil; işletme günü 06:00.
- Anlık görüntü değişmez: gün kapandıktan sonra geriye tarihli yazılan satır anlık görüntüye yansımaz (rapor kovaları ise canlı defterden hesaplandığı için yansır). `occurred_at` çağıran tarafından verildiği için bu fark mümkün.
- Anlık görüntüsü olmayan gün "—" görünür (o güne kadar hareketi olmayan müşteri dahil; gerçekte bakiye 0).
- Sıfır bakiyeli müşteri için de günlük satır yazılır; saklama süresi yok (müşteri x gün büyür).

## Açık risk
- Rapor müşteri başına profil okuyor (N+1) ve defter okuması 500 bin satırda hata verir; küçük işletme ölçeğinde kabul edildi.
- 30/60/90 sınırlarını bir günlük kaydıran mutant testlerde 29/31 gün sınır verisi olmadığı için hâlâ yaşayabilir.
- Boş günler bitmiş sayılmaz (yeni kurulumda iş her turda boş günleri yeniden dener; zararsız, yavaş).
