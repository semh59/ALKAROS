# V1-RMD-345 - CustomerWeb'in hata bölgeleri artık ekran okuyucuya duyuruluyor

- Task ID: V1-RMD-345
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "CustomerWeb sayfalarında hata/durum
bölgeleri `aria-live`/`role=\"alert\"` içermiyor." Doğrulandı: `CustomerWeb`'in üç sayfasının (Menü, Sepet/
OrderEntry, Adisyon/Bill) her birinde `showError()` fonksiyonu bir `#errorState` `<p>` etiketinin
`textContent`'ini değiştirip `hidden`'ini kaldırıyordu — ama bu etiketin hiçbirinde `role`/`aria-live` yoktu,
bu yüzden bir ekran okuyucu kullanıcısı için hata GÖRÜNMEZDİ (sayfayı aktif olarak tarayıp bulmaları gerekirdi).
CustomerWeb'in kendi test altyapısı bugüne kadar sıfırdı (`docs/engineering/e2e-playwright-master-test-plan.md`'nin
kendi notu: "Zero. None at all, not even a unit harness"), bu yüzden bu görev, tam bir E2E paketi kurmak yerine
gerçek bir Chromium tarayıcısıyla doğrudan, gerçek bir doğrulama yaptı (aşağıya bkz.).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Bill/wwwroot/bill.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Menu/wwwroot/index.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.html
- `plan/v1/remediation/V1-RMD-345-customerweb-error-region-aria-alert.md`

## In scope

1. Üç sayfanın her birindeki `#errorState` etiketine `role="alert"` eklendi — bir öğe `role="alert"` ile
   birlikte görünür hale geldiğinde (bu sayfaların kendi `showError()`'ının yaptığı TAM olarak bu: `hidden`
   kaldırılır ve metin AYNI ANDA yazılır), tarayıcılar bunu örtük bir `aria-live="assertive"` bölgesi olarak
   davranır — ayrı bir `aria-live` özniteliği gerekmez.

## Out of scope

1. Diğer durum bölgeleri (`#loadingState`, `#emptyState`/`#emptyBillState`/`#emptyCartState`) — bunlar hata
   değil, sayfa YÜKLENİRKEN zaten DOM'da var olan başlangıç durumları; bir ekran okuyucu normal sayfa
   okumasıyla bunları zaten görür, ayrıca bir canlı bölge bildirimi gerektirmezler.
2. CustomerWeb için kalıcı bir Playwright E2E paketi kurmak — bu, üç sayfanın SIFIR test altyapısını (ne
   birim ne E2E) tek bir küçük erişilebilirlik düzeltmesi kapsamında inşa etmek anlamına gelirdi; bu oturumun
   kendi `docs/engineering/e2e-playwright-master-test-plan.md`'si bunu zaten Faz 6 olarak, ayrı ve büyük bir
   görev olarak planlıyor.

## Dependencies

- None

## Acceptance evidence

- GERÇEK bir Chromium tarayıcısıyla (Playwright, geçici bir betik — doğrulama sonrası silindi) her üç sayfa
  `file://` üzerinden açıldı, sayfanın kendi GERÇEK `showError()` fonksiyonu (global kapsamdaki, sayfanın
  kendi script'i) çağrıldı, ve tarayıcının GERÇEK erişilebilirlik ağacı okundu
  (`page.locator('#errorState').ariaSnapshot()`): üçü de `- alert: <mesaj>` olarak raporlandı (yalnızca HTML
  kaynağında bir öznitelik string'i aranmadı — tarayıcının kendisi bunu bir "alert" olarak sınıflandırdığı
  doğrulandı).
- Mutasyon kontrolü: `role="alert"` öznitelikleri geçici olarak kaldırıldı (`git stash`), aynı gerçek tarayıcı
  kontrolü beklenen şekilde kırmızıya döndü (üçü de `- paragraph: <mesaj>` olarak raporlandı — sıradan, sessiz
  bir paragraf, "alert" değil). `git stash pop` ile geri getirildi, aynı kontrol yeniden `ALL_OK` (üçü de
  `role=alert`) verdi.
- Geçici doğrulama betiği ve tüm ara dosyalar temizlendi.

## Handoff

- None
