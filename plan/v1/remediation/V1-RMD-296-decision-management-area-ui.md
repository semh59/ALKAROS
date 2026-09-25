# V1-RMD-296 - Karar: yönetici ekranları tek 'Yönetim' alanı altında toplanır

- Task ID: V1-RMD-296
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

İstemcisi olmayan yönetici uç noktaları: menüler ve günün menüsü, satın alma, üretim, stok kalemleri/konumları/fire/sayım/yeniden sipariş noktası, stok raporları (gerçek-teorik, kritik stok), roller ve kullanıcılar, gözlemlenebilirlik uyarıları ve sağlık, mutabakat vakaları, ödeme mutabakat/manuel onay raporları, gün sonu, ayar geçmişi, tarif maliyet anlık görüntüleri, güvenlik yönetimi (yedek, RPO, bakım işleri, kilit açma, oturum iptali, tanılama paketi, sipariş yığını kapatma). Yaklaşık 95 uç nokta (yönetici olmayan işlemsel ve dahili uçlar hariç). Bu görev kararı yazar: hepsi tek 'Yönetim' alanı altında, rol tabanlı bölümlerle; her bölüm ayrı bir uygulama görevi olur ve öncelik sırası burada verilir. Semih'in yerleşim sorusuna cevabı bu görev kapatır.

## Owned surface

- `plan/v1/remediation/V1-RMD-296-decision-management-area-ui.md`

## In scope

1. Bölüm listesi, öncelik sırası, gezinme modeli, yetki matrisi, her bölüm için uygulama görevlerinin açılması.

## Out of scope

- Ekranların uygulanması.

## Dependencies

- V1-RMD-266
- V1-RMD-283
- V1-RMD-284

## Acceptance evidence

- Karar kaydı ve bölüm başına açılmış görev listesi; `plan_audit_tool.py validate` temiz.

## Handoff

- None
