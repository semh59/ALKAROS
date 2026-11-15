# V1-RMD-1106 - Satış ekranında arama odağı ve tam eşleşmede otomatik ekleme

- Task ID: V1-RMD-1106
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: React satış ekranında ürün aramasına açılışta odak yok ve barkod/SKU okutulunca kasiyer yine ürün kartına dokunuyor. Satış ekranı açılınca arama alanı odaklanır; arama alanında Enter, yazılan metin bir ürünün SKU'suyla tam eşleşiyorsa (Türkçe i katlamalı) ya da arama tek ürüne indiriyorsa o ürünü ekler (seçenekli ürünse seçenek penceresi açılır) ve alanı temizler. Bekletme ve kalem notu mevcut sunucu sözleşmesinde React satış ekranı için yoktur; bu görevde yapılmaz ve RESULT.md'de raporlanır.

## Owned surface

- `evidence/V1-RMD-1106/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.search-add.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Bekletme; kalem notu; sunucu sözleşmesi

## Dependencies

- V1-RMD-1105

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
