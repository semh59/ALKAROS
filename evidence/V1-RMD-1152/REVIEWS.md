# V1-RMD-1152 bagimsiz qa incelemeleri

## 1. qa-frontend
Kritik/Yuksek yok. Bulgular ve karsiliklari:
- Sayfa yenilenince form yeniden aciliyordu (Orta): DUZELTILDI. `open` parametresi kullanilinca adresten silinir (`history.replaceState`); testi var.
- Baglanti, cekmece kapaliyken olu sokaga goturuyordu (Orta): DUZELTILDI. Baglanti yalniz kendi acik oturumunda ve sunucu cekmece acik + yetki var derse gorunur (`readNoSaleAvailable`); Cashier.tsx'teki ayri yetki kosulu kaldirildi (sunucunun `canKick` yaniti zaten yetkiyi icerir).
- Hata mesaji uc nedeni karistiriyordu (Dusuk): DUZELTILDI. Neden tek tek soylenir (kapali, yetki yok, baska kullanicida, durum okunamadi).
- Sayim ve oturum-yok durumunda sessizlik (Dusuk): DUZELTILDI, ikisinde de aciklama gosterilir.
- Diger ekranlarda baglanti yok (Dusuk): bilincli; karar yalniz satis ekranini istedi.

## 2. qa-ux-tasarim
- Cekmece durumu okunana kadar baslik durumu bekliyordu, yenileme iki istek atiyordu (Orta): DUZELTILDI. Baslik durumu hemen yayinlanir, cekmece durumu ayri ve en yeni okuma kazanir kuraliyla yazilir; testli.
- Gecici hatada baglanti kayboluyordu (Dusuk): DUZELTILDI. Okunamayan cekmece durumu (ag hatasi, hata yaniti) onceki degeri korur; sunucu "kapali" ya da "yetki yok" derse veya oturum kapanirsa baglanti kalkar; testli.
- Baglantida aciklama yoktu (Dusuk): DUZELTILDI, `title`.
- Iptal ve basaridan sonra otomatik donus yok, 5 dokunus (Dusuk): GOREV DISI (plan Out of scope: otomatik yonlendirme); geri baglantisi "Satis ekranina don" der. Acik risk, asagidaki RESULT'ta.
