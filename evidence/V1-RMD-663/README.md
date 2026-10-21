# V1-RMD-663 — Olay zarfı doğrulaması ve mimari test kapsamı

Kapatılan bulgular: 596-B013, B015, B022 ve B011'in Messaging kısmı.

## Plan ile sapma (açık not)
Planın hedef bölümü B019'u (bozuk şifreli zarf için tipli hata) ve B011'in SensitiveData testlerini de sayıyordu. Başlangıç commit'i atıldıktan sonra yeni test dosyasının (`tests/BuildingBlocks/Security/SensitiveData/Encryption/EnvelopeHardeningTests.cs`) düz sahiplik satırı gerektirdiği anlaşıldı; başlamış görev dosyası yalnız Status/Assignee satırlarında değişebildiği için bu iş **V1-RMD-664'e taşındı**. Kod ve yazılmış testler (AesGcmEnvelopeCipher/SensitiveEnvelope dönüşümleri, nonce, anahtar uzunluğu, kurcalama testleri, 4 mutant) 664'te yeniden kanıtlanacak. Bu görev bunları içermez.

## Sonuç
- `OutboxEnvelope` boş toplulaştırıcı kimliğini reddeder (B022; olay yükünde eksik alan doğrulaması yapılmadı).
- Olay serileştirici yorumu "kişisel veri yok" yerine gerçeği söyler: QR serbest metin notu düz JSON olarak outbox'ta durur, 661'deki temizlik süresiyle silinir (B015).
- Yeni Messaging testleri: boş kimlik reddi, eski-önce teslim sırası, vadesi gelmemiş mesaj talep edilmez, geri çekilme iki katına çıkar (B011 Messaging kısmı).
- Mimari modül listesi Payments, QrOrdering, Invoicing, Privacy'yi de izler (B013). Bu, kayıtsız bir kenarı ortaya çıkardı: Host DualScreen kökü KVKK anonimleştirme planını (Privacy) kaydediyor; kenar onaylı listeye ve belge tablosuna eklendi.

## Kanıt
- test-runs.log: Messaging 61/61, Mimari ModuleBoundaries 10/10, temiz derleme 0 uyarı 0 hata.
- dep-*.log: Kitchen OrderItemStateSync 7, QrOrdering PendingOrders 13, Yemeksepeti WebhookInbox 31, QrOrdering RelayCredential 5, OnlineOrdering Host 179, SecurityAdministration Host 30: hepsi yeşil. Bu koşular boş kimlik reddi ile birlikte yapıldı.
- mutations.log: 5 mutant, 5 öldü. Sıra mutantı ilk koşuda hayatta kaldı (ekleme sırası zaten yaşa uyuyordu); test ters sırada ekleyecek şekilde güçlendirildi, sonra öldü (log'da iki satır).

## Bu görevde yapılmayanlar
B019 ve B011'in SensitiveData kısmı (664'e taşındı), B020 (AAD kayda bağlama: mevcut şifreli veriyi okunamaz yapar, sürümlü geçiş gerekir), B025, B026 ve eksik alanlı JSON olay yükü: sonraki görevlerde gerekçesiyle ele alınır.
