# qa-mevzuat-uyum (DAR) — V1-RMD-596 ham rapor özeti

Kapsam: SensitiveData (15 .cs) + Measurements (7 .cs). Kontrol edilen kalem: 48. Çalıştırma yok; hepsi Kod okuması. Kritik/Yüksek yok.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| F1 | Orta (karar) | MaxAgeRetentionPolicyHook.cs:20; IRetentionPolicyHook üretim çağrısı 0 | KVKK saklama kancası ölü yüzey; gerçek imha Privacy.RetentionExecution. Bağlanırsa AuthorizedReEncryptionService her Protect'te yeni CreatedAt damgaladığı için saat sıfırlanır. |
| F2 | Orta (karar) | SensitivePayloadProtector.cs:41-44; SensitiveEnvelope.cs:72-84 | AAD zarfı kayda/amaca bağlamıyor; zarf başka satıra kopyalanırsa çözülür (mağaza bağlaması doğrulanmadı). |
| F3 | Düşük | SensitiveEnvelope.cs:52,56 | Eksik alanlı JSON ArgumentNullException fırlatır; SensitiveDataException'a sarılmıyor. |
| F4 | Düşük | AesGcmEnvelopeCipher.cs:33,65; SensitivePayloadProtector.cs:45,77 | Anahtar/düz metin baytları sıfırlanmıyor. |
| F5 | Düşük (karar) | SensitiveCategory.cs:13-30 | KVKK özel nitelikli veri kategorisi yok. |
| F6 | Düşük | PayloadRedactor.cs:19-21; SensitiveEnvelope.cs:14,37 | Alan adları düz yazılır (teorik). |
| F7 | Düşük | MaxAgeRetentionPolicyHook.cs:14 | maxAge=0 kabul edilir. |
| F8 | Düşük | SensitivePayloadProtector.cs:40 | TimeProvider yok. |
| F9 | Orta | UnitConverter.cs:120-129 vs 168-171 | TryConvert/CanConvert özel dönüşümü kabul eder, Convert UnknownUnitException fırlatır; RegisterUnit üretimde çağrılmıyor. |
| F10 | Düşük | UnitConverter.cs:137-143,176-191 | Aynı boyutlu özel kural sessizce yok sayılır. |
| F11 | Düşük | UnitConverter.cs:80,108-199 | TryConvert tersinirlik doğrulaması yapmaz. |

Doküman sapması: money-tax-business-date.md "kuruş yarım-yukarı" kuralı Measurements'ta yok (para tipi yok; çelişki değil).
Temiz: AES-256-GCM, 12B nonce, fail-closed, metadata AAD, redaksiyon, para tipi yok, birim tablosu tersinir.
Doğrulanamayan: birim sembollerinin mevzuat uyumu, anahtar başına şifreleme sınırı politikası.
