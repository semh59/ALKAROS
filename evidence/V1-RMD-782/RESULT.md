# V1-RMD-782 sonuç

Baseline: 64bcb312 (iki kez kapsam genişledi: 246c9ef7 -> 64bcb312).

## Kök nedenler
1. `TerminalsPanel.tsx` boş `<th />` (jsx-a11y/control-has-associated-label) -> görünür başlık metni `{t.reset}` (qa-frontend: `aria-label` axe `empty-table-header` kuralını geçmiyor; test axe ile doğrular). Kırmızı: `lint-red.log` (2 hata, 15 uyarı; uyarı sınırı 20, projede sıfır-uyarı kuralı yok, dokunulmadı).
2. `CustomerDisplay.tsx` render içinde `Date.now()` (react/purity) -> dakika, kod alındığı anda `pairingMinutes` durumuna yazılır; metin aynı.
3. `.dockerignore` `.git` satırı (V1-RMD-697) bağlamdan `.git`'i çıkarıyordu; `Directory.Build.props` `git rev-parse HEAD` çalıştırdığı için `dotnet publish` her projede kırılıyordu (`docker-build.log`). Satır kaldırıldı; `.git` yalnız `backend-build` aşamasında.
4. `api` imajı açılışta çöküyordu: alpine tabanı invariant globalization, kod `tr-TR` istiyor (`CultureNotFoundException`). `icu-libs` + `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` (yalnız api aşaması).

## Yeşil
- `lint-green.log`: 0 hata. `tsc-green.log` exit 0. `vitest-green.log`: 87 dosya / 824 test geçti.
- `contract-red.log` / `contract-green.log`: yeni 2 sözleşme testi; `.git` testi düzeltmeden önce kırmızı. 4 eski test (compose/ops/dev/secret) bu görevden bağımsız kırık (781'de de kırıktı).
- Imaj derlemesi: `docker-build-4.log` exit 0; tüm imajlar yeni (`alkaros-test` hariç, derlenmedi: yalnız test aracı).
- Yığın `up -d --no-build --wait` ile yeniden oluşturuldu (volume'lara dokunulmadı): `migrations-before.log` 192 kayıt / son 193; `migrations-after.log` 208 kayıt / son 209 (16 yeni migration uygulandı); api healthy.
- Canlı: `live-e2e.log` (sağlık, katalog, taslak, onay, kasaya gönder, hesap, nakit tahsilat hepsi 200); `live-draft-rule.log` (Draft sipariş send-to-cashier = 409 ORDER_NOT_SUBMITTED; QR yönetici listesi 200; QR issue 409 RELAY_NOT_READY = relay henüz etkinleştirilmemiş, beklenen iş kuralı).
- plan-audit 0/0, consistency-audit clean, task-scope exit 0.

## Arayüz
Playwright yalnız giriş sayfasını açtı (`ui-login-page.png`); parola araç çağrısına yazılamayacağı için UI girişi ve UI kasa akışı yapılmadı. Akış API düzeyinde, arayüzün gönderdiği aynı yüklerle doğrulandı. Girişsiz 401'ler beklenen.

## Test verisi (silinmedi)
- KASA-1 masası: sipariş a320e38f-c714-4bf4-b224-f0d713baa2e6 (veritabanında 3 kalem: Izgara Kofte 220 Sent, Adana Kebap 260 Sent, Adana Kebap 260 Draft/NotSent; ikinci taslak çağrısı aynı masa siparişine eklendi). Hesap e3776af7-e236-48e9-888d-a9bb25b29cf6 (480 TL, nakit ödendi, ödeme 404f7be0-f7c5-4206-99fc-4876e56c503f); Draft kalem faturalanmadı.
- S1 masası: Draft sipariş 1d3f1031-93cd-4383-b68a-d0517e2fb5af (gönderilmedi, 260 TL).
- Açık kasa oturumu d43943fc-30cd-4b5a-9b75-fb6f73c30b1e (22:50 UTC'de açık; bu oturumda açılmadı, tahsilat buna yazıldı).
- Stok bakiyeleri satışla düştü.
- Terminal d60b3df9 tarayıcı bağlantısı betik tarafından kuruldu; iki kez kaldırıldı (bir kez SQL ile yönetici sıfırlamasının aynısı, bir kez API ile). Şu an bağlı değil (önceki durum).

## Açık risk
- `.git` build bağlamında (64 MB) yeniden; V1-RMD-697 .env/.git dışlama gerekçesi .env için geçerli kalır.
- icu-libs apk sürümü sabitlenmedi.
- Uyarılar: 15 (Cashier, autoFocus vb.), bu görevin dışında.

## Incelemeler
- qa-frontend: davranış değişmedi, yarış yok; axe uyarısı (aria-label yetmez) doğrulandı ve giderildi (görünür metin + axe testi).
- qa-test-kalitesi: (a) `.dockerignore` testi `.git/`, `**/.git` kalıplarını da reddediyor ve `COPY . .` varlığını doğruluyor (giderildi); (b) ICU testi metin düzeyinde, kanıt canlı açılış (kabul); (c) CustomerDisplay "dakika" testi saflık düzeltmesini ayırt etmiyor (render'da yeniden hesaplama mutantı yaşar) - davranış sözleşmesi değişmedi, açık nokta.
