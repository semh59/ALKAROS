# V1-RMD-1197 - Pilot belgesine seçenekli ürün ekleme ve indirim onayı adımları

- Task ID: V1-RMD-1197
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: documentation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (pilot belgesi sapmaları a, b): `docs/operations/pilot/kasiyer-hizli-baslangic.md` seçenekli ürün eklemeyi (Seçenekler penceresi, zorunlu/isteğe bağlı grup, "Siparişe ekle", seçeneğin sepette, masa panelinde ve fişlerde adıyla görünmesi) ve indirimin yönetici onayı akışını (İndirim ve bahşiş > İndirim uygula > yönetici onayı) anlatmaz. V1-RMD-1192..1196 ekran metnini değiştirdiği için adımlar güncel kodla ve gerçek ekran metniyle yazılır; her alıntı metin kaynakta aranarak doğrulanır.

## Owned surface

- `evidence/V1-RMD-1197/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/operations/pilot/kasiyer-hizli-baslangic.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Seçenekli ürün ekleme ve indirim yönetici onayı bölümleri; metin alıntılarının koddaki karşılıklarıyla doğrulanması (kanıt: arama çıktısı)
2. İki bağımsız qa incelemesi
3. plan-audit ve consistency-audit temiz

## Out of scope

- Denemede belirtilen diğer belge eksikleri (gönderilmemiş ürün, iki iade yolu, Satış yok, Son satışlar yolu); kod

## Dependencies

- V1-RMD-1196

## Acceptance evidence

1. Alıntı doğrulama çıktısı; sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
