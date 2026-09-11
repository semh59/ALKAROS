# V1-RMD-178 - V1-RMD-168..177'nin bağımsız incelemesinde bulunanlar

- Task ID: V1-RMD-178
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

Semih'in isteğiyle ("garson modülüne ait her detayı kontrol edip düzeltme
yaptık mı" sorusu üzerine), V1-RMD-168'den V1-RMD-177'ye kadarki on görev
(bu grubun hiçbiri daha önce bağımsız incelenmemişti — önceki bağımsız
inceleme, V1-RMD-167, yalnızca V1-RMD-156..166'yı kapsıyordu), önceki
bağlamı hiç devralmayan bağımsız bir ajanla derinlemesine incelendi. Ajan
testleri kendi başına yeniden çalıştırdı, sunucu tarafı uygunluk mantığını
elle iz sürdü, güvenlik/doğruluk açılarını kontrol etti. Sonuç: sunucu
tarafı (168, 175, 177'nin sözleşme/uç nokta/repository kısmı) sağlamdı —
tüm eşleşmeler birebir, tüm SQL parametreli, tüm test sayıları
tekrar üretildi. İstemci tarafında 10 gerçek bulgu (D1-D10) çıktı; bu görev
hepsini kapatıyor.

### D1 — HIGH — Escape, PIN kilidinin kendi odak tuzağını serbest bırakıyordu (V1-RMD-173)

`trapBackgroundExcept`/`releaseTrap` tek bir global slottu: options sheet
açıkken cihaz kilitlenirse (idle timeout), `lockScreen()`'in kendi tuzağı
options sheet'inkinin ÜZERİNE yazılıyordu. Options sheet hâlâ `is-open`
sınıfını taşıdığı için (yalnız `inert` edilmişti, kapatılmamıştı), Escape
tuşu hâlâ `closeOptions()`'ı tetikliyor, o da o an aktif olan (kilidin
kendi) tuzağı serbest bırakıp arkadaki tüm uygulamayı (masalar, sipariş
gönder, profil, çıkış) tekrar erişilebilir kılıyordu — kilit ekranı hâlâ
görünürken. Tam olarak V1-RMD-173'ün kapatmaya çalıştığı güvenlik açığının
aynısı, farklı bir tetikleyiciyle hayatta kalmıştı.

Düzeltme: iki tamamlayıcı önlem —
1. `closeOptions()`'ın en başına `if (state.locked) return;` — tek bir
   denetim noktası, Escape/backdrop tıklaması/kapat düğmesi fark etmeksizin
   hiçbir yol kilit ekranı aktifken options sheet'i kapatamaz.
2. `trapBackgroundExcept`/`releaseTrap` gerçek bir **yığın (stack)**
   oldu: her `trapBackgroundExcept` çağrısı yalnız o an HENÜZ inert
   olmayan öğeleri inert yapıp yığına itiyor; her `releaseTrap` yalnız
   kendi ittiği öğeleri geri açıyor (LIFO). İç içe tuzaklar artık birbirini
   ezmiyor — dıştaki tuzağın zaten gizlediği bir öğeyi içteki bir tuzak
   asla "kendi işiymiş gibi" geri açmıyor.

### D9 — LOW — `lastOptionsFocus`, silinecek düğümden yakalanıyordu (V1-RMD-173, D1'le aynı kök alanda)

`openTransferServerSheet()` (V1-RMD-177), profil sheet'i açıkken
`openOptions()`'ı İKİNCİ kez çağırıyor (personel listesi gelene kadar
profil sheet'i açık kalıyor). `openOptions()` her çağrıda
`document.activeElement`'i yakalıyordu — ikinci çağrıda bu, `innerHTML`
ile hemen silinecek olan profil düğmesinin ta kendisiydi. Kapanışta
`lastOptionsFocus.focus()` artık DOM'da olmayan bir düğümü hedefliyor,
odak sessizce `<body>`'ye düşüyordu — V1-RMD-173'ün kapatmaya çalıştığı
tam o hata. Düzeltme: `openOptions()` yalnız sheet KAPALIYKEN açılıyorsa
(`!wasAlreadyOpen`) odak/tuzak yakalıyor; zaten açık bir sheet'i yeniden
doldurmak ikisini de koruyor.

### D2 — MEDIUM — Arka planda başarısız bir katalog yenilemesi menüyü boşaltıyordu (V1-RMD-176)

`loadCatalog()`'un başarısızlıkta `state.products = []` yapması,
yalnız `start()`/`online` olayında çalıştığı sürece zararsızdı (yok
edilecek çalışan bir şey yoktu). V1-RMD-176 onu vardiya ortasında
(`refreshCatalogIfStaleAsync`) fırsatçı şekilde çağırmaya başlayınca, aynı
satır sıradan bir geçici hatada (429, düşen bağlantı) çalışan bir menüyü
siliyordu. Düzeltme: yalnız İLK yükleme (`catalogLoadedAt === 0`) hatada
temizleniyor; daha sonraki bir yenileme hatası son iyi kataloğu olduğu
gibi bırakıyor, bir sonraki fırsatta tekrar deniyor.

### D3 — LOW — "İkram" düğmesinde temel `.btn` sınıfı eksikti (V1-RMD-177)

`class="btn-quiet btn-compact"` — `.btn-quiet` yalnız bir değiştirici
(arka plan/renk/kenarlık); köşe yuvarlama, `display:flex`, `cursor` gibi
her şey `.btn`'de. Düğme köşeli, kalın olmayan, sıradan bir UA düğmesi gibi
görünüyordu. `class="btn btn-quiet btn-compact"` olarak düzeltildi.

### D4 — LOW — `#ribbonQueue` düğme oldu ama hiç stillenmedi (V1-RMD-171)

`<span>`'dan `<button>`'a geçiş işlevsel olarak doğruydu, ama tarayıcının
varsayılan düğme görünümünü sıfırlayan hiçbir kural yoktu — şeridin kendi
renginde küçük gri bir kutu olarak görünüyordu, `--target-min` dokunma
hedefinin de altında. `.ribbon .queue`'ya gerçek kenarlık/arka plan/
`min-height` eklendi.

### D5 — LOW/MEDIUM — Vardiya devri sheet'i eylemin kapsamını yanlış anlatıyordu (V1-RMD-177)

Alt başlık "Bu cihazda açık olan tüm masalar…" diyordu, ama
`TransferServingUserAsync` cihazdan/terminalden bağımsız, o kullanıcıya ait
HER siparişi devrediyor (uç noktanın kendi yorumu bunu zaten söylüyor).
Metin gerçek kapsamı ve geri alınamazlığı söyleyecek şekilde düzeltildi.

### D8 — LOW — Üç durumlu oturum kontrolü iddia ettiğinden daha dar davranıyordu (V1-RMD-172)

`hasValidSession()`'ın kendi yorumu "yalnız gerçekten cevap veren bir
sunucudan gelen 401/403 girişi tetikliyor" diyordu, ama kod
`if (!result.ok) return 'no';` idi — bir 500/503/429 de aynı yola
düşüyordu, tam olarak bu görevin kapatmaya çalıştığı "geçerli oturumu
girişe atma" hatasının aynısı, farklı bir hata sınıfıyla. Düzeltme: yalnız
gerçek 401/403 `'no'`; her şey (ağ hatası dahil) `'offline'` — yorumla
kod artık gerçekten eşleşiyor.

### D6 — LOW — Altı commit, kendi yeni yorumlarını BİR ÖNCEKİ görevin ID'siyle etiketlemiş

`git blame` ile doğrulandı: f792a5dd (gerçek: 169) → "V1-RMD-168" yazmış;
9db32512 (170) → "V1-RMD-169"; 53274c2f (172) → "V1-RMD-171"; dac941b0
(173) → "V1-RMD-172"; 61c7e03b (174) → "V1-RMD-173". `waiter-app.js` ve
`sw.js`'teki toplam 12 yanlış etiketli yorum bloğu, `git blame` her
birinin gerçek commit'ini doğrulanarak tek tek düzeltildi.

### D7 — LOW — V1-RMD-177'nin task dosyası sahip olunan yüzeyi fazla beyan etmiş

`OrderManagementTableDraftTestDatabase.cs`'ye dokunulmuş gibi
listelenmişti; gerçekte hiç dokunulmadı (`GetRequest` yardımcısı
`OrderManagementTableDraftHttpTests.cs`'e eklendi). Task dosyası
düzeltildi.

### D10 — LOW — Service worker'ın ağ-önce mantığında zaman aşımı yarışı yoktu (V1-RMD-169)

Düz `fetch()` bir restoran katının gerçekte baskın olan arıza modunu
(bağlı ama gerçek uplink'i olmayan Wi-Fi — TCP resetlenmek yerine
askıda kalıyor) hızlı başarısız saymıyordu; kabuk yüklemesi anında
önbellekten boyanmak yerine OS bağlantı zaman aşımı kadar donuyordu. Yeni
`fetchWithTimeout(request, 3000)` — ağ 3 saniyede cevap vermezse aynı
önbellek yolunu (sert bir hata zaten kullanıyor) devreye sokuyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-178-independent-review-of-168-177.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css, sw.js
    (V1-WTR-010 sahipliğinde)
  - plan/v1/remediation/V1-RMD-177-comp-and-transfer-server-clients.md
    (D7 düzeltmesi — kendi Owned surface bölümü)

## Out of scope

Yok — bu görev yalnızca bağımsız incelemenin bulduklarını kapatıyor.

## Dependencies

- V1-RMD-177

## Acceptance evidence

- `node --check waiter-app.js`, `node --check sw.js` → temiz.
- `waiter-app.css` parantez dengesi: 243/243 (D4'ün eklediği kural dahil).
- `git blame` ile her düzeltilen V1-RMD yorumu, yazıldığı gerçek commit'le
  tek tek çapraz kontrol edildi (12 yorum, hepsi artık doğru task'a
  atıflı).
- Bu görev yalnız istemci-tarafı JS/CSS ve iki plan/markdown dosyasını
  değiştiriyor; C# tarafında değişiklik yok, bu yüzden mevcut Host test
  paketlerinde regresyon riski yok — yine de daha önceki (V1-RMD-175/177)
  regresyon kanıtlarının değişmediği doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → 0 yeni ihlal.
  (Kalan tek ihlal, `InventoryAdjustmentService.cs:96`, bu görevden önce
  vardı ve sahip olunan yüzeyin dışında.)
- Bu görev için ayrı bir otomatik test eklenmedi — repoda bu dosyalar için
  JS/DOM test altyapısı yok (V1-RMD-173/174/176'da da aynı gerekçeyle
  kaydedildi); her düzeltme kod incelemesiyle (root-cause'un gerçekten
  ortadan kalktığı elle iz sürülerek) doğrulandı.

## Handoff

- None
