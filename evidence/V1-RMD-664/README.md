# V1-RMD-664 — Bozuk şifreli zarf için tipli hata ve zarf testleri

Kapatılan bulgular: 596-B019 ve B011'in SensitiveData kısmı (V1-RMD-663'ten taşındı).

## Sonuç
- Yanlış boyutlu nonce/etiket artık `SensitiveDataEncryptionException`; eksik alanlı kayıtlı zarf (`{}`, `null`) `SensitiveDataException` fırlatır. Ham `ArgumentException` dışarı çıkmaz.
- Yeni testler: 50 şifrelemede 50 farklı nonce, 16/24/31/33 bayt anahtar reddi, kurcalanmış nonce ve etiket, yanlış boyutlu nonce/etiket, eksik alanlı zarf.

## Kanıt
- test-runs.log: SensitiveData 36/36, temiz derleme 0 uyarı 0 hata.
- dep-*.log: RelaySettings 9, PublicGateway 26, Yemeksepeti WebhookInbox 31, Token TerminalCredential 6, QrOrdering RelayCredential 5, DataProtectionRetention 43, SecurityAdministration Host 30: hepsi yeşil.
- mutations.log: 4 mutant, 4 öldü (dönüşümler geri alınınca, sabit nonce, 16 bayt anahtar kabulü).

## Yapılmayanlar
B020 (AAD kayda bağlama: mevcut şifreli veriyi okunamaz yapar, sürümlü geçiş ister), B025, B026: sonraki görevde gerekçesiyle.
