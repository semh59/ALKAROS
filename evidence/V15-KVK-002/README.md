# V15-KVK-002 - Mağazalar arası anonimleştirme

`kvkk-retention --apply` artık saklama süresi dolan beş veri sınıfının hepsini (personel, sipariş notu, rezervasyon nedeni, müşteri, tedarikçi) tek akıştan geçirir:
her kayıt bir iş (`privacy.anonymization_jobs`), her mağaza bir adım. Adımın yazımı, kontrol noktası ve olayı aynı işlemde kaydedilir; yarıda kalan iş ilk bitmemiş mağazadan devam eder.
Son adımdan sonra her mağaza artık kişisel veri kalıntısı için taranır; yalnız temiz kayıt saklama listesinde "bitti" olur.

## Değişiklikler

- `src/Modules/Privacy/Anonymization/**`: iş akışı motoru (`PostgresAnonymizationWorkflow`), alan eylemlerini dışarıdan alan `IAnonymizationPlans` bağlantı noktası. Modül kendi şemasından başkasına yazmaz.
- `src/Host/Program.cs`: onaylı alan eylemleri (`KvkkAnonymizationPlans`) ve komutun akışa bağlanması. Eski ayrı personel/not/rezervasyon silme kodu kaldırıldı (tek seçim, tek yazım motoru).
- Müşteri: şifreli iletişim zarfı hiçbir anahtarla açılamayan değerle değişir, açık talep `Anonymized` olur. Tedarikçi: ad maskelenir, telefon ve e-posta silinir; vergi no ve dairesi kalır.
  Hesap defteri, fatura alıcı verisi ve tutarlar dokunulmaz. Bakiyesi sonradan sıfırdan farklı olan müşteri bekletilir (`blocked=N`).
- Migration `172` (`privacy.anonymization_jobs/checkpoints/events`): kontrol noktaları ve olaylar yalnız eklenir, iş silinemez; kişisel veri tutmaz.
- Bağlantılar: `ModuleRegistry`, `ALKAROS.Host.csproj`, `ALKAROS.slnx`, sınır testleri, kilit dosyaları, `order.json`, `MigrationManifest`, belge satırı ve runbook.

## Kanıt

- `tests-module.log`: 9 motor testi yeşil (kontrol noktası + olay, yarıda kalıp devam, tekrar çalıştırma, yasal tutma, çalışırken konan tutma, bekletme, kalıntı, atlanan kayıt, ekleme-yalnız iz).
- `mutation-workflow.log`: motorda 5 mutant (bitmiş adımı tekrarlama, son kontrol yok, bekletme yok, tutma yakalanmıyor, atlama ters) ve Host eyleminde 1 mutant (tedarikçi telefonu kalıyor) kırmızı; dosyalar geri alındı, `fc /b` özdeş.
- `tests-host.log`, `tests-architecture.log`: Host test projesi (komut testleri dahil, yeni müşteri/tedarikçi/bakiye/defter testi) ve modül sınır testleri.
- `migration-up-down.log`: tüm migration'lar, 172 geri alma ve yeniden uygulama, ikinci uygulama (idempotent).
- `gercek-deneme.log`: gerçek Postgres'te komutun kendisi: kuru çalıştırma hiçbir şeyi değiştirmez; `--apply` müşteriyi ve tedarikçiyi temizler, vergi no/dairesi ve defter kalır; ikinci çalıştırma sıfır iş; denetim izi.

## Açık kalan

- `audit.audit_events` 10 yıl sonra silinmesi bölüm bırakmayı gerektirir; `V1-RMD-481` olarak planlandı.
- Müşteri anonimleştirme talebi akışı (`CustomerAnonymizationService`) ayrı kaldı; bu akış saklama süresi dolan kayıtlar içindir.
- Hesap defteri notları (yalnız eklemeli) ve fatura alıcı zarfı yasal saklama gereği değiştirilmez; bu liste hukuki onaya (avukat/muhasebeci) bağlıdır.
