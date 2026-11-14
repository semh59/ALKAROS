# Reviews
- qa-ux-tasarim: KABUL (440px kartta tasma/hiyerarsi sorunu yok; 2 dusuk not: font kisaltmasi/em ve JS satir ici stil gorunmuyordu).
- qa-test-kalitesi: ilk tur DUZELTME (Orta): 'font-size :' bosluklu, buyuk harf, calc/var/anahtar sozcuk ve 'font :' regex'ten kaciyordu. Test yeniden yazildi (her font bildirimi duz px/rem >= 14 olmali, kisaltma yasak, JS/HTML'de boyut/zoom/small yok), mutasyon listesi 12 mutanta cikti (hepsi killed) ve hepsi son testle yeniden kosuldu. Dusuk 6/7 (textarea/input 15px, tek stil dosyasi) dogrulandi: sorun yok.
