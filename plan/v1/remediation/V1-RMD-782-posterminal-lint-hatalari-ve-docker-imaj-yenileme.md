# V1-RMD-782 - PosTerminal lint hataları giderilsin, Docker imajları bugünkü kodla yenilensin

- Task ID: V1-RMD-782
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-781 raporu: Docker imaj derlemesi `pnpm build` içindeki `pnpm run lint` adımında iki hatayla kırılıyor, bu yüzden canlı yığın 2026-10-17 imajlarıyla çalışıyor ve sonraki işler (QR yönetimi, Draft kuralı, seçenek zorunluluğu, menü sayaçları vb.) canlıda yok.

1. `TerminalsPanel.tsx`: tablo başlığındaki boş `<th />` (`jsx-a11y/control-has-associated-label`) erişilebilir ad taşımıyor. Eylem sütununa görünmez ama okunabilir ad verilir.
2. `CustomerDisplay.tsx`: eşleştirme kodunun "kaç dakika sonra yenilenir" metni render içinde `Date.now()` çağırıyor (`react/purity`). Dakika sayısı, eşleştirme kodu alındığı anda hesaplanıp durumda tutulur; metin ve davranış aynı kalır.

3. Lint düzeltilince imaj derlemesi ikinci bir kök nedene takılıyor: `.dockerignore` `.git` satırı (V1-RMD-697) build bağlamından `.git` klasörünü çıkarıyor, oysa `Directory.Build.props` içindeki `ResolveAndValidateRepositoryCommit` hedefi konteyner içinde `git rev-parse HEAD` çalıştırıyor ve `fatal: not a git repository` ile her csproj'u kırıyor (`dotnet publish` exit 1). V1-RMD-098 `.git`'in build provenance için bağlamda tutulduğunu kayda geçirmişti. `.git` satırı kaldırılır (`.env` dışlamaları kalır); `.git` yalnız `backend-build` aşamasında bulunur, çalışma imajlarına `COPY --from` ile geçmez. Bu bir regresyon testiyle korunur.

4. Imaj derlenince `api` konteyneri açılışta çöküyor (`CultureNotFoundException: tr-tr is an invalid culture identifier`): `aspnet:8.0-alpine` tabanı `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` ile geliyor (ICU yok), oysa `DualScreenApplication.CustomerAccounts.cs:32`, `CustomerAccountsStore.cs:27`, `DualScreenApplication.Payments.cs:639` ve diğerleri `CultureInfo.GetCultureInfo("tr-TR")` kullanıyor; 17 Ekim imajı bu kodu içermediği için o zaman görünmedi. `api` aşamasına `icu-libs` kurulur ve `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` yapılır (kod değişmez). Regresyon testi Dockerfile'ı denetler; kanıt canlı açılış.

Ardından imajlar bugünkü HEAD ile yeniden derlenir, yığın mevcut volume'lar korunarak yeniden oluşturulur, yeni migration'ların uygulandığı doğrulanır ve kasa akışı canlıda denenir.

## Owned surface

- `evidence/V1-RMD-782/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management-security-system/ (V1-RMD-450) — boş sütun başlığına ad, test.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx (V1-RMD-097) — render içi saat okuması kaldırılır.
  - .dockerignore (V1-RMD-697) — yalnız `.git` dışlamasının kaldırılması.
  - tests/Deployment/test_container_contract.py (V1-RMD-098) — `.git` bağlamda kalır ve api imajında ICU var sözleşmeleri.
  - deploy/docker/Dockerfile (V1-RMD-098) — yalnız `api` aşamasına icu-libs ve küreselleştirme ortam değişkeni.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. İki lint hatasının kök nedenle giderilmesi; kural kapatma veya bastırma yok; `pnpm run lint` 0 hata; `vitest` ve `tsc` yeşil.
1a. `.dockerignore` `.git` satırı kalkar; `test_container_contract.py` yeni testi `.git` dışlanırsa kırılır.
2. Başka dosyada lint hatası çıkarsa yalnız o hata için plan genişletilir.
3. Docker imajlarının yeniden derlenmesi, servislerin volume silmeden yeniden oluşturulması, migration uygulandığının doğrulanması.
4. Canlı doğrulama: sağlık, katalog, taslak, onay, kasaya gönderme, hesap, nakit ödeme; Draft siparişin kasaya gönderimi 409 `ORDER_NOT_SUBMITTED`; yönetici QR uç noktaları erişilebilir; tarayıcıda kasa akışı.

## Out of scope

- Mevcut uyarılar (limit `--max-warnings=20`, şu an 15), `strings.ts`, kural yapılandırması, volume silme, test verisi temizliği.

## Dependencies

- V1-RMD-781

## Acceptance evidence

1. Kırmızı: lint 2 hata. Yeşil: lint 0 hata, vitest ve tsc exit 0.
2. Imaj derlemesi exit 0; yığın sağlıklı; migration kayıtları bugünkü sürümde.
3. Canlı akış gerçek yanıt kodlarıyla `evidence/V1-RMD-782/` altında.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-frontend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.

## Handoff

- None
