# V1-RMD-1020 - Masa panelinde mutfaksiz eklenen kalem isaretlenir

Duzeltme: contracts.ts TableOrder kalemine addedWithoutKitchenReason (sunucu OrderItemDto zaten gonderiyor); TableOrderBridge kalem satirina "Kasada eklendi (mutfağa gitmedi)" notu.
Kanit: yeni test once kirmizi; TableOrderBridge+workspace 77/77; client-build-gate exit 0; gercek tarayici M-10: "1 x Su · Kasada eklendi (mutfağa gitmedi)" (masa-paneli-kasada-eklendi.png). Mutasyon 1/1 killed.
Qa sonrasi: Draft ve kasada eklenen kalem tek not alir (test+mutant 2/2); TableOrderBridge 26/26; gate exit 0.
