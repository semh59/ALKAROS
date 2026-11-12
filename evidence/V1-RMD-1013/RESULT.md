# V1-RMD-1013 - Hizli satis sekmesi masa sepetini birakir

Kok neden: ustteki Hizli satis sekmesi her zaman aria-current=page idi ve yol zaten "/" oldugundan tiklamasi masa sepetini birakmiyordu.
Duzeltme: Cashier.tsx - masa sepetindeyken sekme secili degil; tiklayinca leaveTableCart (isareti birakir, siparisi yeniden okur) ile temiz hizli satis.
Kanit: 2 yeni test once kirmizi, sonra yesil; Cashier* 19 dosya 103/103; client-build-gate exit 0 (lint, tsc, vite build); gercek tarayici: masa M-01 sepetinde sekme secili degil ("Masa M-01 - Siparis"), sekmeye basinca baslik "Yeni satis", sekme secili (masa-sepeti-sekme.png, hizli-satis-temiz.png). Mutasyon 2/2 killed.
Qa sonrasi: Masalar sekmesi de masa sepetinde secili gosterilir (mutant 3/3 killed); masa sepeti yokken sekme tiklamasi gereksiz istek atmaz. Son kosu: Cashier* 103/103, client-build-gate exit 0.
