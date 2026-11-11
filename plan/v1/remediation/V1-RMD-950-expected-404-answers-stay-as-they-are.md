# V1-RMD-950 - Beklenen durumlardaki 404 yanıtları (aktif kasa oturumu yok, iş günü yok, kat planı yok) olduğu gibi kalsın

- Task ID: V1-RMD-950
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: decision
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): tarayıcı konsolu, kasa oturumu yok / iş günü açılmamış / kat planı yok gibi beklenen durumlarda 404 satırı yazıyor. Değerlendirme: bu satırı tarayıcı ağ katmanı yazar, uygulama kodu değil; 404, "bu kayıt yok" için doğru ve istemcilerin 20 yerinde durum olarak okunan sözleşmedir. 200 ve boş gövdeye çevirmek sözleşmeyi bozar ve her tüketiciyi değiştirir. Karar: sözleşme değişmez; gerekçe ve doğrulama kanıt dosyasına yazılır.

## Owned surface

- `evidence/V1-RMD-950/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. İstemcilerin 404 yanıtını nasıl okuduğunun taranması ve kararın yazılması
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-949

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
