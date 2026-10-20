# qa-guvenlik-yetki — V1-RMD-596 ham rapor özeti

105/105 kalem. Probe (tmp/qa/V1-RMD-596/qa-guvenlik-yetki/probe) çalıştırıldı: 7 senaryo exit 0 (G3 kanıtı).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| G1 | Yüksek | QrOrderIntegrationEvents.cs:41 Notes; QrPendingOrderStore.cs:124; IntegrationEventSerializer.cs:7 | QR müşterisinin serbest metin notu (alerji/sağlık içerebilir) şifresiz düz JSON olarak outbox payload'ına yazılıyor; serileştirici yorumu "kişisel veri yok" diyor. |
| G2 | Yüksek | outbox/inbox için DELETE yok; 003 TTL yok | dispatched/dead satırlar hiç silinmez; KVKK saklama süresi yok; G1 verisi süresiz kalır. |
| G3 | Orta | AesGcmEnvelopeCipher.cs:78; SensitiveEnvelope.cs:56 (Çalıştırıldı) | Bozuk zarf SensitiveDataException yerine ArgumentException/ArgumentNullException fırlatır (fail-closed ama tip sözleşmesi bozuk). |
| G4 | Orta | SensitiveEnvelope.cs:64 | AAD'ye kayıt/amaç bağlamı girmiyor; DB yazma yetkisi olan saldırgan zarfı başka kayda kopyalayabilir. |
| G5 | Düşük | AesGcmEnvelopeCipher.cs:34 | Rastgele 96-bit nonce için kullanım sayısı sınırı/rotasyon tetikleyicisi yok (çıkarım). |
| G6 | Düşük | AesGcmEnvelopeCipher.cs:33,65; SecretValue.cs:32 | Anahtar/düz metin baytları sıfırlanmaz. |
| G7 | Düşük | EnvironmentVariableSecretProvider.cs:21 | abc-d, ABC_D, abc_d aynı ortam değişkenine eşlenir; erişim politikası ada göre karar verirse çakışma. |
| G8 | Düşük | UnauthorizedSensitiveReadException.cs:11 | Accessor adı istisna mesajında. |
| G9 | Düşük | OutboxAdministration.cs:131 | Requeue gerekçesi operatörden alınmıyor (sabit reason). |
| G10 | Düşük (doküman) | OutboxEnvelope.cs:5 vs IntegrationEventSerializer.cs:8 | Yorumlar çelişir; kod düz JSON. |
| G11 | Düşük | testler | Ad eşleme, nonce benzersizliği, G3 durumları için test yok. |
| G12 | Düşük | SecurityModule.cs:48, CustomerDataModule.cs:33, OnlineOrderingModule.cs:44, QrOrderingModule.cs:32 | ISecretProvider kaydı dört modülde tekrar (görev "üç" demişti). |

Temiz: AES-GCM kullanımı, sır çözümleme erişim sırası, log/hata sanitize, outbox yönetim yetkisi ve audit, SQL allowlist.
Doğrulanamayan: bağımlılık zafiyet taraması (ağ yok), security.manage rol eşlemesi, eski last_error satırları, nonce çakışma hacmi.
