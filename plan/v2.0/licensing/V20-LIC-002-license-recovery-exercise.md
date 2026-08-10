# V20-LIC-002 - Exercise license recovery

- Task ID: V20-LIC-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:II.2.24
- PDF:III.26

## Goal

Veri kaybı olmadan lisansın sona ermesi, doğrulama hatası ve yetkili yenileme için onaylanmış operasyonel kurtarma
yolunu kanıtlayın.

## Owned surface

- `release/evidence/licensing/**`
- Bu görev licensing implementation kodunu değiştiremez.

## In scope

- Sona erme uyarısı, çevrimdışı yetki tükenmesi, geçersiz imza, saat anormalliği, yetkili yenileme ve kurtarma denetimi.
- V20-LIC-001 `NotApplicable` ise aynı karar kanıtıyla bu task da `NotApplicable` kapanır ve recovery çalıştırılmaz.

## Out of scope

- Lisans politikasının değiştirilmesi, ticari lisansların verilmesi ve production müdahalesi.

## Dependencies

- V20-LIC-001
- V15-RUN-001

## Deliverables

- Senaryo metni ve operatör kurtarma kanıtı.

## Acceptance evidence

- İlgili her arıza, belgelenmiş güvenli duruma ulaşır ve yetkili kurtarma, mali, stok veya denetim geçmişini
  değiştirmeden hizmeti geri yükler.
- `V20-LIC-001` kanıtlı `NotApplicable` ise aynı decision ID, tarih ve approver kaydedilir; recovery exercise
  çalıştırılmaz ve bu task da `NotApplicable` kapanır.
- `V15-RUN-001` kanıtlı `NotApplicable` ise runbook kaynaklı kurtarma senaryoları beklenmez; kalan arıza senaryoları
  güvenli durum ve geri yükleme kuralına yine uyar.

## Handoff

- V20-GAT-002
