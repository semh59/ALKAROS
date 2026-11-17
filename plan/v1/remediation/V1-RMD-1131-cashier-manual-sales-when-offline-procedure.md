# V1-RMD-1131 - Kasada internet kesilince elle satış prosedürü (go-live belgesi)

- Task ID: V1-RMD-1131
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: documentation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 17: kasada çevrimdışı satış bilinçli olarak kodlanmayacak. Bunun yerine `docs/operations/production-go-live-checklist.md` içine Türkçe, sade bir bölüm eklenir: "Kasada internet kesilince elle satış prosedürü". İçerik: ne zaman kullanılır (kasa ekranı sunucuya ulaşamıyor ve yönetici kesintiyi doğruladı), defter formatı (sıra no, saat, ürünler ve adet, tutar, ödeme türü, kasiyer adı), internet gelince nasıl işlenir (yönetici gözetiminde, tek tek, kasa oturumu açıkken, defter satırı işlendi diye işaretlenir), riskler (stok, kasa farkı, çifte giriş, fiş yok, kart ödemesi kesintide alınamaz) ve kasiyer için 6 adımlık kısa liste. Kod, şema ve test değişmez; "Go / no-go summary" tablosuna bir satır eklenir.

## Owned surface

- `evidence/V1-RMD-1131/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/operations/production-go-live-checklist.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Belge bölümü ve özet tablo satırı
2. İki bağımsız qa incelemesi
3. plan-audit validate ve consistency-audit temiz

## Out of scope

- Çevrimdışı satış kodu; kasa ekranı; sunucu

## Dependencies

- V1-RMD-1127

## Acceptance evidence

1. plan-audit ve consistency-audit çıktıları
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
