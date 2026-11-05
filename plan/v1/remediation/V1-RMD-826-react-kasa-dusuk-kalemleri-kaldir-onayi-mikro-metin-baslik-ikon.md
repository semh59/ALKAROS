# V1-RMD-826 - React kasa düşük kalemleri: kalem Kaldır onayı, mikro metin ve kontrast, dar ekranda başlık menüsü, ayrı menü ikonları, bölge seçiminde çift istek

- Task ID: V1-RMD-826
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G7c kalemlerinden React kabuğu parçası (`3-tasarim.md` T-15, T-19, T-20, T-23, T-24, T-21; `evidence/V1-RMD-739` F0905). PO kararı (2026-11-04): en küçük hata kritik sayılır, açık bırakma yok. Kodda doğrulanan durum:

1. T-20 `Cashier.tsx`: "Kaldır" kalemi tek dokunuşla siparişten siler; siparişteki satır modifikatörlerini ekran yeniden kuramaz (satır verisinde ürün/seçenek kimliği yok), bu yüzden geri alma değil iki adımlı onay: ilk dokunuş satırı "Kaldırılsın mı?" durumuna alır ("Evet, kaldır" ve "Vazgeç", 48 px); başka satır, vazgeç ya da sipariş değişince onay kapanır.
2. T-15/T-19 `styles.css`: mikro metinler 9-11 px (`.58rem`-`.7rem`) ve açık zeminde düşük kontrastlı (`#8a949d`, `#a6786e`, `#82909b`, `#81909c`). Düzeltme: en az 12 px (`.75rem`), her çift en az 4,5:1; bir test bu kuralları CSS'ten okur.
3. T-23 sol menüde "Hesap", "Bekleyen hesaplar" ve "Online yemek" aynı simgeyi, "Sistem", "Yetki" ve "Yönetim" aynı simgeyi kullanır (`workspace.tsx`). Düzeltme: her öğe için ayrı simge (mevcut simge setinden: bekleyen hesaplar saat, online gönder, yetki kilit, yönetim kasa); başlık eylemlerinin tek stili (`.header-action`) zaten uygulanıyor, test bunu kilitler.
4. T-24 dar ekranda (<= 600 px) satış ekranı başlığı sekiz eylemle ekranın dörtte birini kaplar. Düzeltme: dar ekranda ikincil eylemler "Menü" düğmesinin açtığı bir listeye toplanır (Kasa, Bekleyen hesaplar ve Çıkış başlıkta kalır).
5. F0905 `workspace.tsx`: bölge seçimi hem `handleSelectZone` içinde hem `load` yeniden çalışarak kat planını iki kez ister. Düzeltme: doğrudan çağrı kalkar, `load` bölge değişince tek istek atar.
6. T-21 (bekleyen hesap ana etiketi masa/saat) V1-RMD-821'de kapandı; kanıt RESULT.md'de.

## Owned surface

- `evidence/V1-RMD-826/**`
- `src/Clients/PosTerminal/src/routes/Cashier.low-items.test.tsx`
- `src/Clients/PosTerminal/src/contrast.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — Kaldır onayı, dar ekran menüsü.
  - src/Clients/PosTerminal/src/routes/workspace.tsx (V1-CUI-006) — menü simgeleri, bölge seçimi.
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx (V1-CUI-006) — simge ve tek istek testleri.
  - src/Clients/PosTerminal/src/styles.css (V1-RMD-042) — mikro metin, kontrast, menü stili.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — yeni cümleler.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.
3. Dokunulan ekranların mevcut testleri yeşil kalır.

## Out of scope

- Sunucu, veritabanı, geçiş; vanilla kasa (V1-RMD-825).

## Dependencies

- V1-RMD-825

## Acceptance evidence

1. PosTerminal vitest tam paket yeşil; oxlint ve tsc temiz; kırmızı sonra yeşil: Kaldır iki adım ister ve vazgeçilebilir, mikro metin boyutları ve kontrastlar, menü simgeleri tektir, dar ekranda ikincil eylemler menüdedir, bölge seçimi tek kat planı isteği atar.
2. Playwright E2E (satış ekranı özellikleri) yeşil.
3. Mutasyon: her yeni davranış bozulunca ilgili test kırılır; `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir, düzeltme sonrası doğrulama turu.
6. Semih'in elle deneyeceği senaryo: satış ekranında bir kalemde "Kaldır"a bas; "Kaldırılsın mı?" sorar. Telefon genişliğinde başlık "Menü" düğmesiyle kısalır. Masalarda bölge seçince sayfa bir kez yüklenir.

## Handoff

- None
