# V1-RMD-139 - Independent audit: NFC origin isolation and JS bundle splitting (O2)

- Task ID: V1-RMD-139
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09, QR/NFC müşteri sipariş yüzeyi turu)
O2 bulgusu: `/nfc/{tableId}` sayfası tüm PosTerminal (Cashier/admin)
bundle'ıyla aynı origin'den serviş ediliyordu — sunucu tarafı yetkilendirme
zaten doğru çalışıyordu (bir NFC ziyaretçisi Cashier API'lerini asla
çağıramazdı), ama (a) origin'in kendisi hiçbir şeyi ayırmıyordu ve (b)
müşterinin tarayıcısı ihtiyacı olmayan tüm Cashier/admin JS kodunu
indiriyordu. Kullanıcıyla görüşülüp ikisi de (müşteri-ekranı deseninin
aynısı origin izolasyonu + build-time code-splitting) istendi.

**Önemli bulgu, kayda geçirildi:** customer-display'in kendi "origin
izolasyonu" (deep-analysis finding B-4) yalnızca API katmanında çalışıyor
— `CustomerDisplay.tsx` de `NfcOrder.tsx` gibi tek bir PosTerminal React
bundle'ının içindeki bir route; statik dosya sunumu origin'e göre hiç
ayrılmıyor. Yani bu deseni birebir kopyalamak tek başına JS indirme
sorununu çözmüyordu — bu yüzden ikinci kısım (code-splitting) ayrıca
gerekliydi.

## Owned surface

- `plan/v1/remediation/V1-RMD-139-nfc-origin-isolation-and-bundle-splitting.md`
  (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/DualScreen/DualScreenOptions.cs, DualScreenApplication.cs
    (V0-ARC-009/deep-analysis B-4 sahipliğinde) —
    `CustomerDisplayUrl`/`CustomerDisplayOriginHeader` desenini birebir
    ayna alan yeni `NfcOriginUrl`/`NfcOriginHeader` seçenekleri
    (`--nfc-urls`/`--nfc-origin-header` CLI bayrakları), port/mutual-
    exclusivity doğrulamaları ve `/api/v1/nfc/*`'i ayıran ikinci bir
    origin-gate middleware'i eklendi. Var olan display-gate mantığına
    dokunulmadı; yalnız `nfcUris`, `allUris`/port-çakışma kontrollerine
    dahil edildi.
  - src/Clients/PosTerminal/src/App.tsx (V1-WTR-.../PosTerminal genel
    routing sahipliğinde) — beş route de artık `React.lazy` +
    `Suspense` ile yükleniyor; yönlendirme mantığının kendisi
    (path eşleştirme) değişmedi.
  - src/Clients/PosTerminal/src/stale.test.ts (aynı sahiplik) —
    `renderApp` yardımcı fonksiyonu, artık asenkron olan lazy-yüklemeyi
    doğru şekilde bekleyecek şekilde güncellendi (her "settle" turu
    kendi `act()` çağrısında, React'ın ara commit'leri gerçekten DOM'a
    yansıtması için); test senaryolarının kendisi değişmedi.
  - tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs,
    tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs
    (aynı sahiplikler) — yukarıdaki değişiklikleri doğrulayan yeni
    testler.
  - tools/consistency-audit/consistency_audit.py (paylaşılan denetim
    aracı) — dinamik `import(...)` çağrısının argümanı da statik
    `import ... from "..."` gibi bir modül yolu (kullanıcıya görünen
    metin değil); kural buna göre küçük, hedefli bir istisna kazandı
    (aşağıya bakın).

## In scope

1. **Backend API origin izolasyonu.** `NfcOriginUrl`/`NfcOriginHeader`
   (`--nfc-urls`/`--nfc-origin-header`), `CustomerDisplayUrl`/
   `CustomerDisplayOriginHeader` ile birebir aynı doğrulama kuralları
   (port çakışması `--urls` VE `--customer-display-urls` ile kontrol
   ediliyor; `--api-only` ile `--nfc-urls` karşılıklı dışlanıyor;
   `--nfc-origin-header` `--api-only` gerektiriyor). Yeni middleware:
   NFC origin'inde yalnız `/api/v1/nfc/*` sunuluyor, ana origin'de
   `/api/v1/nfc/*` sunulmuyor. **Hiçbiri configure edilmedikçe
   (varsayılan, bugünkü tüm dağıtımlar dahil) middleware hiç eklenmiyor
   — tam geriye dönük uyumlu, tıpkı customer-display gibi.**
2. **Frontend code-splitting.** `App.tsx`'teki beş route
   (`Cashier`/`CustomerDisplay`/`NfcOrder`/`RelaySettings`/
   `ReservationStation`) artık `React.lazy(() => import(...))` ile
   yükleniyor — Vite build çıktısı doğrulandı: her route artık kendi
   ayrı chunk'ında (`NfcOrder-*.js` 4.84kB, `Cashier-*.js` 14.93kB,
   `workspace-*.js` 109.34kB, `CustomerDisplay-*.js` 63.91kB,
   `RelaySettings-*.js` 5.96kB) — bir NFC ziyaretçisi artık yalnız
   paylaşılan giriş chunk'ını (`index-*.js`) ve kendi `NfcOrder`
   chunk'ını indiriyor, ~200kB'lık Cashier/CustomerDisplay/RelaySettings
   kodunu asla.
3. `tools/consistency-audit/consistency_audit.py`'deki gerçek bir
   yanlış-pozitif düzeltildi: dinamik `import("./routes/Cashier")`
   çağrısı, statik `import`'un zaten sahip olduğu "bu bir modül yolu,
   kullanıcıya görünen metin değil" istisnasını kazandı.

## Out of scope

- **Gerçek prod dağıtımına `--nfc-urls`/`--nfc-origin-header`'ı fiilen
  bağlamak** (Caddyfile'a `nfc.<ALKAROS_PROXY_HOST>` diye yeni bir
  virtual host eklemek, Dockerfile'ın CMD argümanlarını güncellemek,
  README'yi güncellemek — customer-display'in `deploy/docker/**`'ta
  yaptığı gibi). Mekanizma hem backend'de hem frontend'de tam ve test
  edilmiş durumda, ama gerçekten devreye almak (yeni bir alt alan adı/
  DNS/sertifika kararı, NFC etiketlerinin zaten fiziksel olarak
  programlandığı URL'yi bu yeni origin'e göre ayarlamak) ayrı bir
  dağıtım kararı — bu görev kapsamında sessizce yapılmadı.
- Denetimin daha önce kapatılan diğer bulguları (K1 → V1-RMD-137, Y1 →
  V1-RMD-136, O1/D2/D3 → V1-RMD-138).

## Dependencies

- V12-NFC-003

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Host.Experience.Composition.Tests`: **6/6** (yeni
    `NfcOriginOnlyExposesTheNfcRoutesAndTheMainOriginRefusesThem`
    dahil — ana origin NFC API'sine 404, NFC origin Cashier API'sine
    404, her origin kendi API'sine erişebiliyor).
  - `ALKAROS.Host.Tests` (tam migration/composition/reachability
    paketi, yeni `DualScreenOptionsTests` senaryoları dahil):
    **126/126** (öncekinden +5).
- `node_modules/.bin/tsc --noEmit`: hata yok.
- `node_modules/.bin/vitest run`: **22 dosya, 136 test, hepsi geçti**
  (regresyon yok; `stale.test.ts`'in iki testi lazy-loading'i doğru
  bekleyecek şekilde güncellendi, 3 tekrarda kararlı).
- `node_modules/.bin/vite build`: başarılı; çıktı beş ayrı route
  chunk'ı gösteriyor (yukarıya bakın) — code-splitting fiilen
  doğrulandı, sadece varsayılmadı.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi); dinamik-import yanlış-pozitifi
  düzeltildi, yeni gerçek ihlal yok.

## Handoff

- None
