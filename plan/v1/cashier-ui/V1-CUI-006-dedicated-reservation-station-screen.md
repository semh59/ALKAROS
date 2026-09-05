# V1-CUI-006 - Dedicated Reservation Station screen

- Task ID: V1-CUI-006
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`V1-TBL-008`'in kaydettiği kümenin ekran halkası: `reservations
.dedicated_station_enabled` açıkken (`V1-SET-003`), PosTerminal'de
`/reservations` altında, müşteri ekranı gibi kendi URL'i olan, sade bir
"Rezervasyon İstasyonu" ekranı — yalnız kat planı + rezervasyon aksiyonlarını
gösterir, satış/hesap/mutfak sekmeleri yok. Müşteri ekranından farkı: o
kimliksiz eşleştirmeyle çalışır, bu ekran ise yetkili aksiyon aldığı için
mevcut kasiyer personel girişini (kullanıcı adı/parola) yeniden kullanır.
Yeni izin kodu yok — sunucunun zaten hesapladığı `AllowedCommands` listesi,
operatörün rolü `tables.reserve` tutuyorsa rezervasyon aksiyonunu, tutmuyorsa
hiçbir şeyi gösterir; ekranın var olup olmaması bunu değiştirmez.

**Bu görev sırasında bulunan ve giderilen gerçek kusur:** `workspace.tsx`'teki
`/`, `/tables`, `/billing`, `/kitchen` rota erişim kontrolleri hâlâ
`pos.cashier.mutate`'i kontrol ediyordu. Bu izin migration 049'da
(`V1-IAM-024`, bu oturumdan önce) kataloktan tamamen kaldırıldı — hiçbir
oturum bir daha bu izni hiç tutamaz. Sonuç: `canOpenRoute` her zaman `false`,
ve gezinme çubuğunun kendi `requiredCapability` filtresi de aynı dizeyi
kullandığından, Satış ekranı dışındaki HER PosTerminal ekranı (Masalar,
Hesap bölme, Mutfak) hem menüden gizli hem doğrudan URL ile erişilemez
haldeydi — hiçbir kullanıcı için, sessizce. Granüler karşılıklarına
düzeltildi: `/` ve `/tables` → `orders.create`/`tables.status`, `/billing`
→ `bills.split`, `/kitchen` → `orders.send` (sunucunun kendi endpoint'lerinin
gerçekte istediği kodlarla birebir).

## Owned surface

- `plan/v1/cashier-ui/V1-CUI-006-dedicated-reservation-station-screen.md`
- `src/Clients/PosTerminal/src/routes/ReservationStation.tsx`
- `src/Clients/PosTerminal/src/routes/ReservationStation.test.tsx`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Clients/PosTerminal/src/routes/workspace.tsx` (mevcut sahiplikte
  kalır — bkz. dosya geçmişi) — `pos.cashier.mutate` kusuru granüler
  kodlara düzeltildi (yukarıya bakın); `TableRoute` bu ekranın yeniden
  kullanabilmesi için `export` edildi; mevcut hiçbir davranış başka
  şekilde değişmedi.
  `src/Clients/PosTerminal/src/routes/workspace.test.tsx` (mevcut
  sahiplikte kalır) — düzeltmeyi kanıtlayan yeni bir `describe` bloğu
  eklendi (granüler izin varken erişilebilir, kaldırılmış
  `pos.cashier.mutate` ile yasak); mevcut testler değişmedi.
  `src/Clients/PosTerminal/src/App.tsx` (mevcut sahiplikte kalır) —
  `/reservations` önekinin yeni ekrana yönlendirilmesi, `/display`
  ile aynı desende.
  `src/Clients/PosTerminal/src/routes/Cashier.tsx` (mevcut sahiplikte
  kalır) — `runtimeConfig`'ten `reservationStationEnabled` okunur; açıksa
  kasa ekranı başlığında "Rezervasyon istasyonu" bağlantısı (müşteri ekranı
  bağlantısıyla aynı desen, yeni pencerede açar); kapalıysa hiç görünmez.
  `src/Clients/PosTerminal/src/contracts.ts` (mevcut sahiplikte kalır) —
  `RuntimeConfiguration`'a `reservationStationEnabled: boolean` alanı
  eklendi.

## In scope

- `/reservations` altında yeni bir üst seviye ekran (`/display` ile aynı
  desende `App.tsx`'te dispatch edilir): personel girişi (kasa ekranıyla
  aynı `api.login`/`api.session`/`api.logout`), giriş sonrası
  `runtime-configuration`'daki bayrağı kontrol eder — kapalıysa net bir
  "bu ekran etkin değil" mesajı gösterir (boş veya bozuk bir ekran değil),
  açıksa `workspace.tsx`'in mevcut `TableRoute`'unu (kendi `RouterProvider`'ı
  içinde) `canManage={false}` ile render eder — bölge/masa oluşturma ve
  sipariş başlatma bu ekranın işi değil, yalnız rezervasyon.
- Kasa ekranındaki "Rezervasyon istasyonu" bağlantısı (yukarıdaki sınırlı ek).
- `workspace.tsx`'in `pos.cashier.mutate` kusurunun düzeltilmesi (yukarıdaki
  sınırlı ek) — bu görevin kapsamına, `TableRoute`'u dışa aktarırken aynı
  dosyada bulunduğu için dahil edildi.

## Out of scope

- Yeni bir izin kodu veya rol — `tables.reserve` (zaten var) kullanılır.
- Own-check / waiter-order servis-atama modeli — rezervasyon bununla ilgili
  değil.
- Bölge/masa/kat planı yönetimi bu ekrandan — kasiyer ekranının işi
  (`canManage={false}`).

## Dependencies

- V1-SET-003

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck`, `test`, `build`: hepsi
  sıfır çıkış kodu. `test`: 19 dosya / 118 test (115 → 118: yeni
  `ReservationStation.test.tsx` 3/3 — oturumsuz personel girişi gösterir,
  anahtar kapalıyken "etkin değil" mesajı gösterir, anahtar açıkken
  girişten sonra masa çalışma alanına geçer; `workspace.test.tsx`'e eklenen
  6 yeni test `pos.cashier.mutate` kusurunun düzeltmesini kanıtlıyor —
  gerçek granüler izinle `/tables`/`/billing`/`/kitchen` erişilebilir,
  yalnız kaldırılmış `pos.cashier.mutate` ile yasak).
- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata
  (bu görev kendisi .NET dosyası değiştirmiyor; `V1-SET-003`'ün
  `runtime-configuration` değişikliğiyle birlikte tam çözüm derlemesi
  doğrulandı).
- `ALKAROS.Architecture.Tests` 8/8, `ALKAROS.Host.Experience.Composition
  .Tests` 4/4 — regresyonsuz.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/project-manifest/project_manifest_tool.py`: VALID.
- **Ortam istisnası:** `pnpm install` başlangıçta `node_modules/typescript`
  içinde eksik bir `bin/` dizini nedeniyle bozuktu (muhtemelen önceki bir
  kesintiye uğramış kurulum); `pnpm store prune` + temiz `node_modules`
  silme + yeniden `pnpm install` ile giderildi — kod değişikliği değil,
  ortam kaynaklı, düzeltildi ve doğrulandı.

## Handoff

- V1-GOV-076
