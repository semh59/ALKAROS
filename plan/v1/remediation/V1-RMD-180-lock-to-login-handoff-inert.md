# V1-RMD-180 - PIN kilidinden girişe geçişte kalıcı inert kalma

- Task ID: V1-RMD-180
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin bir başka
bulgusunu kapatır: kilit ekranı (`lockOverlay`) aktifken herhangi bir
sebeple `showLogin()` çağrıldığında — en sık görülen yol, garsonun
yanlış PIN girmesi; `api()`'nin kendi genel 401 yakalayıcısı ZATEN her
401'de `showLogin()`'i çağırıyor, `submitPin()`'in kendi 423 dalına özel
bir durum bile değil — giriş ekranı görünür oluyor ama tıklanamaz
kalıyordu.

Kök neden: `lockScreen()` kendi `trapBackgroundExcept(lockOverlay)`
çağrısıyla `loginOverlay`'i (o an gizli, aktif değil) inert yapıyordu.
`showLogin()`'in eski idempotency kontrolü yalnızca "giriş zaten
görünüyor mu"yu kapsıyordu — kilidin kendi hâlâ serbest bırakılmamış
tuzağını hiç hesaba katmıyordu. `showLogin()` `loginOverlay.hidden`'ı
`false` yapıp kendi yeni tuzağını (`trapBackgroundExcept(loginOverlay)`)
kuruyordu, ama `loginOverlay`'in üzerindeki ESKİ `inert` bayrağını hiç
temizlemiyordu — o zaten aktif eleman olarak yeni tuzağın dışında
tutuluyordu, dolayısıyla bir daha asla `inert=false` yapılmıyordu.
Sonuç: giriş formu görünür ama görünmez bir `inert` altında, `<body>`
tüm pointer olaylarını yutuyor — garson şifresini bile yazamıyor.

Kanıt: `tests/E2E/WaiterPwa/specs/` altına geçici bir repro spec'i
(`zz-temp-lock-to-login-handoff.spec.js`, commit edilmedi) eklendi:
gerçek profil sheet'i üzerinden PIN kuruldu, ekran kilitlendi, TEK bir
yanlış PIN girildi (423'e kadar beklemeye gerek yok — `api()`'nin genel
401 yakalayıcısı zaten devreye giriyor). Düzeltme YOKKEN test, giriş
formunun `#loginUsername` alanına tıklanamadığını (`<body> intercepts
pointer events`, 3s zaman aşımı) kanıtladı. Düzeltme VARKEN aynı test
1/1 passed. (`git stash` ile düzeltme geçici kaldırılıp aynı test
yeniden çalıştırılarak doğrulandı.)

## Owned surface

- `plan/v1/remediation/V1-RMD-180-lock-to-login-handoff-inert.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/js/auth.js (V1-WTR-039 sahipliğinde) —
    `showLogin()`'in en başına, `state.locked` iken kilidin kendi
    tuzağını serbest bırakan bir `releaseTrap()` çağrısı eklendi; bu,
    kilidin daha önce inert yaptığı her şeyi (loginOverlay dahil) geri
    açıp, sonrasında kurulan yeni giriş tuzağının doğru bir taban
    üzerine inşa edilmesini sağlıyor.

## Out of scope

- V1-RMD-181..185 ve sonrası: audit'in kalan bulguları — ayrı görevler.
- `submitPin()`'in 423 dalının kendi ayrı `showLogin()` çağrısı: kök
  düzeltme `showLogin()`'in kendisinde olduğu için, bu çağrıyı kaldırmak
  gerekmiyor — zaten idempotent ve doğru artık.

## Dependencies

- V1-WTR-039
- V1-RMD-178

## Acceptance evidence

- Geçici repro spec (`zz-temp-lock-to-login-handoff.spec.js`, commit
  edilmedi):
  - Düzeltme YOKKEN (`git stash` ile geçici kaldırılarak): 1/1 FAILED,
    gerçek kanıtla (`#loginUsername`'a tıklama 3 saniyede zaman aşımına
    uğradı — `<body> intercepts pointer events`).
  - Düzeltme VARKEN: 1/1 passed.
  - Test dosyası doğrulama sonrası silindi; kalıcı kapsamın bir parçası
    değil.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  npx playwright test` (`tests/E2E/WaiterPwa`), gerçek Postgres + gerçek
  Host binary + gerçek Chrome: **18/18 yeşil, 3 ardışık çalıştırmada**
  (bu değişiklik her testin kendi `login()` yardımcısının geçtiği
  `showLogin()`'e dokunduğu için özellikle tekrar tekrar doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
