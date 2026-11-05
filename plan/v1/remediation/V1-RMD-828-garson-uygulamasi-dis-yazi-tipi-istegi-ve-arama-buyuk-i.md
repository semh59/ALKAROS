# V1-RMD-828 - Garson uygulaması: dış yazı tipi isteği kalkar, ürün aramasında "I" ve "ı" eşleşir

- Task ID: V1-RMD-828
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

1. `src/Clients/WaiterPwa/wwwroot/index.html` Google Fonts'a üç dış istek atıyor (`fonts.googleapis.com`, `fonts.gstatic.com`): çevrimdışı çalışması gereken uygulama her açılışta dışarı çıkıyor, KVKK açısından IP sızdırıyor ve yazı tipi gelene kadar yazı atlıyor. Düzeltme: üç satır kalkar; `waiter-app.css` zaten sistem yazı tipine düşen bir yığın tanımlıyor (`Inter, ui-sans-serif, system-ui, ...`). Test, uygulamanın hiçbir sayfa/stil/betiğinde dış adres (http/https) bulunmadığını kilitler.
2. F0432 (`evidence/V1-RMD-739`): ürün aramasında `toLocaleLowerCase('tr-TR')` büyük noktasız "I"yı "ı" yapar; İngilizce klavyeyle "isparta" yazan garson "Isparta" ürününü bulamaz. Düzeltme: iki taraf da tr-TR küçültülür ve "ı" "i"ye katlanır; "İ" zaten "i" olur.

## Owned surface

- `evidence/V1-RMD-828/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/WaiterPwa/wwwroot/index.html (V1-WTR-001) — dış yazı tipi satırları kalkar.
  - src/Clients/WaiterPwa/wwwroot/js/screens/menu.js (V1-WTR-055) — arama katlaması.
  - tests/Clients/StaticApps/waiter-pwa-search-and-fonts.test.js (V1-RMD-109) — dış istek ve arama testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları.

## Out of scope

- Kasa ve React ekranlarındaki aynı arama katlaması (V1-RMD-835).

## Dependencies

- V1-RMD-803

## Acceptance evidence

1. StaticApps testleri yeşil: garson uygulaması dosyalarında dış adres yok; "isparta", "ISPARTA", "ıspar", "İstanbul"/"istanbul" ürünleri bulur; tam paket yeşil.
2. Mutasyon: font satırı geri gelirse ve katlama kalkarsa test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
