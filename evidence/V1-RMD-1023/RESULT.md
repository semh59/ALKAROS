# V1-RMD-1023 - Dar ekranda yan menu etiketleri gorunur

Duzeltme: shell.css 768-1279 px araliginda menu baglantisi ikon ustte etiket altta (0.75rem = 12 px (qa-ux onerisiyle 0.72 -> 0.75; overflow-wrap break-word)) dizilir; rozet kosede; etiket gizleme kurali kaldirildi.
Kanit: yeni shellStyle.test.ts 2 test eski CSS ile kirmizi (2 failed), yeni CSS ile yesil; shell + workspace testleri yesil; client-build-gate exit 0; gercek tarayici 1024x768 ve 768x1024: Masalar, Bekleyen hesaplar (rozet 5), Diger gorunur, etiket tasmasi yok (yan-menu-1024x768.png, yan-menu-768x1024.png). Mutasyon 2/2 killed.
