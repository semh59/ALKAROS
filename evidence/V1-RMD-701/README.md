# V1-RMD-701 - Terminal kimliğinin sunucuda doğrulanması

Kapanan bulgular: B035, B088 (aynı kök neden: terminal kimliği yalnız bir ad, kimseyi doğrulamıyordu).

## Ne değişti

- Migration 202: `customer_display.terminals.terminal_key_hash` (boş = henüz sahiplenilmemiş) ve 64 haneli büyük harf onaltılık özet CHECK'i. Down betiği ters işlem.
- `DualScreenStore.TerminalKey.cs`: tek `UPDATE ... WHERE terminal_key_hash IS NULL` ile sahiplenme (aynı anda gelen tarayıcılardan yalnız biri kazanır), sonra özet karşılaştırma (sabit süreli). Yalnız özet saklanır.
- Giriş (`/api/v1/auth/login`): parola doğrulandıktan sonra, oturum açılmadan önce. İlk giriş terminali sahiplenir ve tarayıcıya `alkaros.terminal-key.<terminalId>` çerezini verir (HttpOnly, SameSite=Strict, 5 yıl). Anahtarsız veya yanlış anahtarlı giriş 403 `TERMINAL_KEY_MISMATCH` ve Türkçe mesaj alır, oturum açılmaz. Mevcut terminaller ilk girişte anahtarı devralır. Yanlış parola terminalin durumunu açığa çıkarmaz (önce 401).
- Yönetici: `GET /api/v1/management/security/terminals` (bağlı mı, son giriş yapan, son giriş zamanı) ve `POST .../terminals/{id}/reset-binding` (security.manage, denetim kaydı `security.terminal-binding-reset` aynı işlemde). Güvenlik sekmesinde yeni "Terminaller" paneli.
- B088: kasa oturum çerezi zaten terminal kimliğine bağlı (`cashier:<id>`); terminal artık doğrulandığı için ayrı bir doğrulama gerekmedi.

## Kanıt

- Gerçek PostgreSQL: `TerminalKeyTests` 8 test, giriş HTTP testleri 2, yönetici HTTP testi 1 (`logs/targeted*.log`, `logs/security-admin.log`); istemci 22 vitest testi (`TerminalsPanel.test.tsx`, `SecuritySection.test.tsx`). Tam Host.Tests 325 testten 324 geçti; kalan biri 201 numaralı göçün geri alma testiydi (artık 202 sonrası olduğu için "son göç" varsayımı bozuldu) ve düzeltilip yeşil koşuldu (`logs/migration-tests.log`; 202 için ileri-geri göç testi eklendi, down betiği boşaltılan mutant öldü). SecurityAdministration 34/34, OfflineReconciliation 13/13, ApiConventions 5/5, ModuleBoundaries 11/11.
- Mutasyon: `mutate.mjs`, `mutations.log` - 17 mutant, 17'si KILLED. İlk koşuda S3 (eşzamanlı sahiplenme koruması), S14 (istemci rota yolu) ve S17 (panelin bölüme eklenmesi) hayatta kaldı; S3 için sorgu tek koşullu `UPDATE`'e çevrildi ve sıralı test mutantı yakalar hale geldi, S14/S17 için istemci testleri eklendi, üçü yeniden koşuldu ve öldü.

## Açık riskler

- Yönetici bir terminali sıfırladığında o terminaldeki açık kasa oturumu kapatılmaz; yalnız sonraki girişin tarayıcı bağlaması sıfırlanır.
- Sıfırlamadan sonra terminali ilk giren tarayıcı sahiplenir; yönetici sıfırlamayı terminalin başında yapmalı.
- Tarayıcı verisini silen kullanıcı, yönetici sıfırlayana kadar o terminalde giriş yapamaz (PO kararı).
- Cihaz kayıt/onay akışı (yeni terminalin yönetici onayıyla eklenmesi) kapsam dışı, ayrı ürün işi.
