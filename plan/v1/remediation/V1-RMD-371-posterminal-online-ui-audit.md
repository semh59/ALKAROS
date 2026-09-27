# V1-RMD-371 - PosTerminal Online (hub/menu/ops/credentials/problems/store-status) modül denetimi: CSS sınıf uyumsuzluğu

- Task ID: V1-RMD-371
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 11. modülü:
PosTerminal'in Online Yemek bölümü — altı alt-özellik (`online-hub`, `online-menu`,
`online-operations`, `online-platform-credentials`, `online-problems`, `online-store-status`,
toplam ~2633 satır). On iki boyut üzerinden tarandı.

Bu bölüm bu oturumun en olgun köşelerinden biri çıktı: `OnlineFoodHub.tsx`'in kendi sekme
sistemi (`role="tablist"`/`"tab"` + `aria-selected` + `aria-controls` + `role="tabpanel"`) TAM
ve doğru — hatta bu oturumda düzeltilen diğer sekme desenlerinden (Modül 1/5/9) daha ileri
düzeyde (`aria-controls` bağlantısı onlarda bile yoktu). Altı alt-özelliğin HER BİRİ kendi özel
hata sınıfını (`OnlineHubApiError`, `OnlineMenuApiError`, vb.) zaten doğru kontrol ediyor —
Modül 7/8/9'un sistemik "yanlış hata sınıfı" bulgusu burada YOK. Altı alt-özelliğin HER BİRİ
zaten kendi axe-core taramasına sahip — Modül 5/10'un test-kapsama boşluğu da burada YOK. Filtre
düğme grupları (`online-ops__filters`, `online-menu__platforms`) zaten `aria-pressed` taşıyor.

Tek gerçek bulgu, sistematik CSS/JS sınıf adı uyumsuzluğu taramasında bulundu — Modül 1'in kök
neden deseniyle (`tab-chip`/`category-tab-btn`) birebir aynı sınıftan: `OnlineOperationsWorkspace.tsx`
"Müşteri notu" (müşteriye ulaşmak için telefon/kod içeren, kasiyerin yüksek sesle okuması gereken
bir bilgi) `online-ops__note` sınıfıyla render ediliyordu, ama `online-operations.css`'te bu isimde
bir kural YOKTU — yalnızca farklı, ilgisiz bir amaç için kullanılan `.online-ops__notice` vardı.
Sonuç: bu önemli bilgi tamamen stilsiz (varsayılan tarayıcı `<p>` görünümü) render ediliyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/online-operations.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/OnlineOperationsWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-371-posterminal-online-ui-audit.md`

## In scope

1. **[T1/T5, Orta — erişilebilirlik/tutarlılık] `.online-ops__note` sınıfının hiçbir CSS kuralı
   yoktu.** Bu, mevcut, ilgisiz `.online-ops__notice`'a birleştirilmedi (o, farklı bir bağlamda
   — genel çalışma alanı bildirimi — zaten doğru kullanılıyor); bunun yerine kendi, sipariş
   kartı içine uygun bir çağrı-kutusu stili eklendi.

## Out of scope

- `online-store-status`'un durum değiştirme düğmeleri (Aç/Meşgul/Bugün kapat) kasıtlı olarak
  `aria-pressed` almadı — bunlar bir görünüm/filtre seçici DEĞİL, her tıklaması gerçek bir
  sunucu mutasyonu tetikleyen eylem düğmeleri; zaten geçerli durumun düğmesi `disabled` ile
  işaretleniyor.
- Ürün-katmanı (P1-P4) gözlemi yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37
  dosya, 276 test, mevcut bir teste eklenen yeni assertion dahil): 276/276 geçti, regresyon yok.
- Mutation-check: `online-operations.css` `git stash` ile geri alındı, genişletilmiş test
  GERÇEKTEN kırmızı oldu. `git stash pop` ile geri yüklendi, paket tekrar 276/276 yeşile döndü.
- **Yöntem notu:** ilk yazımda `getComputedStyle` ile gerçek CSS uygulamasını sınamaya
  çalışıldı — jsdom gerçek stil sayfalarını uygulamadığı için bu assertion, CSS düzeltmesi geri
  alınmışken de YEŞİL kaldı (sahte-pozitif, yakalanıp düzeltildi). Bunun yerine ham CSS kaynağını
  okuyup `.online-ops__note` kuralının GERÇEKTEN var olduğunu doğrulayan statik bir kontrol
  kullanıldı — jsdom tabanlı Vitest testlerinde bu sınıf-adı-uyumsuzluğu türü bulguyu doğrulamanın
  doğru yolu budur (Cashier vanilla'nın Playwright E2E testlerinin gerçek tarayıcıda
  `getComputedStyle` kullanabilmesinin aksine).

## Handoff

- None
