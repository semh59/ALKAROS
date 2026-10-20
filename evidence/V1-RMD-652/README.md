# V1-RMD-652 kanıtı

- Test: `test-runs.log` (CustomerData.AnonymizationState 36, temiz derleme, 0 uyarı); ilk yeşil koşu `green.log`.
- `AStateChangeWhoseAuditRecordCannotBeWrittenIsNotKept`: denetim yazılamazsa talep oluşmaz, anonimleştirme tamamlanmaz (durum Pending kalır), yeniden deneme tamamlar ve tek `CustomerAnonymized` olayı yazılır.
- Mutasyon: `mutations.log` 4/4 yakalandı, dosya geri alındı (aynı).
- Kırmızı kanıt: yeni imza eski kodda yok; kırmızı yerine mutasyon kanıtı.
- Not: profil alanlarının silinmesi ayrı depoda (`ICustomerProfileStore`) kendi işleminde yapılır; yeniden denemede profil zaten anonim olduğundan yalnız durum ve denetim yazılır. Bu yüzden denetim hatası sonrası profil anonim ama talep Pending görünebilir; yeniden deneme bunu kapatır (iz kaybı yok).
- Kalan risk: performans bütçesi yönetimi, gönderilmiş kalem iptali, elle yazılmış kayıt satırları ayrı görevlerde.
