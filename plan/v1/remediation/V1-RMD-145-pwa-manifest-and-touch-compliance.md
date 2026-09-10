# V1-RMD-145 - PWA manifest'leri tableti ve markayı taşımıyor

- Task ID: V1-RMD-145
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in tablet ve kiosk soruları (2026-09-10) üzerine yapılan taramada
istemci kabuğunda beş somut kusur bulundu: her iki PWA manifest'i cihaz
yönünü sabitliyor (garson `portrait-primary`, kasa `landscape-primary`), ikisi
de marka dışı renkler taşıyor (`#0f172a`/`#020617`), kiosk için gereken
tam ekran görüntü kipi hiç istenmiyor, kasa ve PosTerminal'in viewport
meta'sında `viewport-fit=cover` yok, ve kasa arama alanı 44px yükseklikle
DESIGN.md'nin 48px dokunma hedefi kuralını çiğniyor. Bu görev yalnız kabuğu
düzeltir; ekranların kendi yeniden tasarımı (`docs/design/foundations.md`
Faz 1) ayrı iştir.

## Owned surface

- `plan/v1/remediation/V1-RMD-145-pwa-manifest-and-touch-compliance.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Clients/WaiterPwa/wwwroot/manifest.json (V1-WTR-00x sahipliğinde)
  - src/Clients/Cashier/wwwroot/manifest.json (V1-CUI-00x sahipliğinde)
  - src/Clients/Cashier/wwwroot/index.html (V1-CUI-00x sahipliğinde) —
    yalnız viewport meta satırı.
  - src/Clients/Cashier/wwwroot/cashier-app.css (V1-CUI-005 sahipliğinde) —
    yalnız dokunma hedefi token'ı, arama alanı ve kategori sekmesi
    yüksekliği.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.css (V1-WTR-006 sahipliğinde) —
    yalnız bölge çipi yüksekliği. Uygulama sırasında tarandığında bu
    dosyanın da 48px altında bir dokunma hedefi taşıdığı görüldü; aynı
    kural ihlali olduğu için kapsam bu tek kuralla genişletildi.
  - src/Clients/PosTerminal/index.html (V1-RMD-02x sahipliğinde) — yalnız
    viewport meta satırı.

## In scope

1. **Yön kilidi kaldırılıyor.** Sabit `orientation` alanı iki manifest'ten de
   siliniyor. Cihaz yönünü kullanıcının nasıl tuttuğu belirler: bir tablet
   yatay, aynı uygulama telefonda dikey çalışabilmeli. Kiosk için yön
   sabitlemesi gerekirse bu, çalışma anında `screen.orientation.lock()` ile
   ve o cihazın kendi kipine göre yapılır — manifest'te sabitlenmez.
2. **Marka renkleri.** `theme_color` ve `background_color`
   `docs/design/foundations.md` §1'in `--color-ink` `#0B2135` değerine
   çekiliyor; bugünkü `#0f172a`/`#020617` eski palettendir ve açılış ekranı
   ile sistem çubuğunu logodan farklı gösterir.
3. **Kiosk için tam ekran istenebilir hâle geliyor.** `display_override`
   ile önce `fullscreen`, desteklenmezse mevcut `standalone` davranışı
   korunuyor (`display` alanı `standalone` olarak bırakılıyor, böylece eski
   tarayıcılarda davranış değişmiyor).
4. **`viewport-fit=cover`.** Kasa ve PosTerminal'in viewport meta'sına
   ekleniyor; garson zaten taşıyor. Bu olmadan çentikli/yuvarlak köşeli
   ekranlarda `env(safe-area-inset-*)` hiç çalışmaz.
5. **48px altındaki her dokunma hedefi.** Kasada `--touch-target` token'ı
   44px'ten 48px'e düzeltilip gerçekten kullanılıyor — bugüne kadar
   tanımlıydı ama hiçbir kural ona başvurmuyordu (ölü token). Dört gerçek
   ihlal kapatılıyor: kasa adet artır/azalt düğmesi (28×28px — en kötüsü),
   kasa kategori sekmesi (38px), kasa arama alanı (44px) ve garson bölge
   çipi (40px). Dördü de servis boyunca sürekli dokunulan kontroller. Tarama, iki dosyada da 48px altında başka dokunma hedefi
   kalmadığını doğrulayarak kapanıyor.

## Out of scope

- Ekranların duyarlı (tablet) düzeni: `waiter-app.css` ve `cashier-app.css`
  tek bir `@media` kuralı taşımıyor, ama bu CSS'ler Faz 1 yeniden tasarımında
  zaten baştan yazılacak — bugünkü dosyaya tablet düzeni eklemek atılacak
  koda yatırım olur.
- Kiosk kilidi (hareketsizlikte inen perde, Wake Lock, uzun basmayla açma):
  istemci davranışıdır, ekran yeniden tasarımıyla birlikte gelir.
- Oturum güvenliği: `DeviceSessionService.DefaultLifetime` 30 gün ve
  hareketsizlik kilidi yok. Gerçek risk budur ve PIN'li oturum kilidi ayrı
  bir karar/görev gerektirir.

## Dependencies

- None

## Acceptance evidence

- Her iki `manifest.json` geçerli JSON olarak parse edildi ve beklenen
  alanları taşıdığı doğrulandı: `orientation` alanı yok, `theme_color` ve
  `background_color` `#0B2135`, `display` `standalone`, `display_override`
  `["fullscreen", "standalone"]`.
- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- İki istemci CSS'i 48px altındaki her `width`/`height`/`min-height` için
  tarandı. Kalan dört eşleşmenin hiçbiri dokunma hedefi değil: `.pulse-dot`
  (8px, dekoratif durum noktası) ve `.ticket-row-qty`/`.cart-item-qty`
  (20/24px `min-width`, yalnız rakam hizalaması için — tıklanabilir değil,
  `<span>`). Dokunulabilir hiçbir kontrol 48px'in altında kalmadı.
- Semih'in elle deneyebileceği senaryo: garson PWA'yı bir tablete ana ekrana
  ekle ve tableti yatay tut — uygulama artık dikeye zorlamıyor; açılış
  ekranının ve sistem çubuğunun logonun lacivertiyle aynı olduğunu gör;
  kasada arama alanına parmakla dokunarak 48px hedefi doğrula.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok.

## Handoff

- None
