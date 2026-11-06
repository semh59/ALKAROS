# V1-RMD-838 sonuç

Migration 218 ile kapatılanlar (uygulanmış migration dosyaları sağlama toplamlı olduğundan düzeltme yeni dosyada):
- Tek bir hesap kalemini silmek ona bağlı denetimli ayar/bölüşüm satırını sessizce siliyordu (`ON DELETE CASCADE`); kısıtlar `NO ACTION` oldu: kalem tek başına silinemez, hesabın tamamı silinince zincir sürer. Üretim kodunda kalem silme yolu yok (iptal `superseded_at` ile işaretler), davranış değişmedi.
- 209'un `NOT VALID` bıraktığı `ck_bills_nonnegative_amounts` ve `fk_bills_cancelled_by` doğrulandı; ihlalli eski satır varsa migration kısıt adıyla durur.
- `cash.cash_transactions` ekleme-yalnız oldu: `UPDATE` ve `DELETE` reddedilir (23000). Tek istisna kullanıcı silinirken yabancı anahtarın `recorded_by` alanını boşaltması (kayıtlı kullanıcı artık yoksa izinli; doğrudan `recorded_by = NULL`, kayıtsız satırın güncellenmesi ve toplu silme de reddedilir).

## Doğrulama
- Kırmızı (`red.log`): 4 yeni test düştü.
- Yeşil: Host Migrations+Manifest 49/49 (son hâl) (`green-host.log`); tam sıra migration kullanan Host Billing 210/210 (`green-Billing.log`), CashSession 106/106 (`green-CashSession.log`); PaymentTender ve CustomerAccounts paketleri çalıştırılmadı (kasa defterine yalnız ekleme yaparlar).
- Mutasyon (`mutations.log`): 9 mutant (iki kalem anahtarı CASCADE'e dönüş, iki doğrulamanın kaldırılması, kayıtsız satır güncellenebilir, doğrudan boşaltma serbest, silme serbest, kullanıcı silme reddi, toplu silme koruması başka tabloda) hepsi öldürüldü, `cmp` ile geri doğrulandı.
- Atlanan test yok.

## Kapsam dışı bırakılan
211'in `NOT VALID` kısıtı (eski satırlarda gerekçe olmayabilir) doğrulanmadı. 180 down'ının çakışan `bill_number`ta çökmesi: eski satırları silmeden düzeltilemez, betikte yazılı; kayıt koordinatöre raporlandı.

## İncelemeler (qa-veritabani, qa-test-kalitesi)
Giderilen: tetikleyici derinliği (`pg_trigger_depth`) yerine "kayıtlı kullanıcı artık yok" koşulu (tetikleyici içinden boşaltma açığı kapandı); `TRUNCATE` koruması eklendi ve testli; kayıtsız satır güncellemesi testli. İkinci incelemenin "hayatta kalan m5" bulgusu, ilk tetikleyici tasarımından kalan eski kayıttı (tasarım değişince mutant yeniden adlandırıldı); eski günlükler silindi, güncel 9 mutantın hepsi öldürüldü.
Kabul edilen: UPDATE dalı yalnız yabancı anahtarın SET NULL eylemi sırasında erişilebilir (kullanıcı satırı silinmiş olmak zorunda), diğer kolonlar ayrıca karşılaştırılmıyor; uygulama rolü şemalarda SELECT/UPDATE/DELETE yetkili (`deploy/docker/roles.sql`), tetikleyici `identity.users` okuyabilir; `down` doğrulanmış kısıtları eski `NOT VALID` hâline döndürmez (davranış değişikliği riski yok); büyük tablolarda anahtar yeniden kurma kilidi ölçülmedi (veri küçük).
