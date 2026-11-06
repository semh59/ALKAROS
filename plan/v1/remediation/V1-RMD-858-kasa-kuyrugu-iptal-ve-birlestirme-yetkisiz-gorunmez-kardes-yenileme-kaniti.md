# V1-RMD-858 - Kasa kuyruğu: iptal ve birleştirme düğmeleri yetkisiz kullanıcıya gösterilmez; kardeş sipariş yenilemesi kanıtlanır

- Task ID: V1-RMD-858
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9/G11 artığı: (1) Bekleyen hesaplar ekranı (PendingChecksWorkspace) her satırda 'Siparişi iptal et' ve birleştirme kutusunu yetkiye bakmadan gösteriyor; sunucu iptali `bills.cancel`, birleştirmeyi `bills.split` ister, yetkisiz kasiyer düğmeye basınca hata alır. Oturumun yetkileri istemcide zaten var (workspace.tsx capabilitySet; iade paneli aynısını kullanıyor), sunucuya yeni alan gerekmez: ekran bu iki yetkiyi alıp ilgili denetimleri göstermez. (2) 'Tek sipariş iptali kardeş siparişleri kuyruğa bildirmiyor' iddiası: sunucu yalnız iptal edilen siparişin kimliğini duyurur, ama istemci her PendingChecksChanged olayında listenin tamamını yeniden okur (PendingChecksWorkspace.tsx:104); kardeş (birleştirilmiş faturanın öbür siparişi) satırının yenilendiği davranış testiyle kanıtlanır, kanıtlanırsa iddia çürütülür, çıkmazsa düzeltilir. (3) 'Müşteri ekranı ve kuyruk çift görünüm' kaydı tanımsız: hangi iki görünümün çakıştığı kayıtta ve kodda belirlenemedi, ürün sorusu olarak ana oturuma bildirilir.

## Owned surface

- `evidence/V1-RMD-858/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx — canCancel ve canMerge özellikleri; yetkisiz denetim gizlenir.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx — davranış testleri: yetki ve kardeş yenileme.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.cancel.test.tsx — iptal testlerinin yeni özelliğe uyarlanması.
  - src/Clients/PosTerminal/src/routes/workspace.tsx — oturum yetkilerinden bills.cancel ve bills.split aktarımı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı testler: yetkisiz iken iptal düğmesi ve birleştirme kutuları yok; yetkili iken var; bir olay sonrası kardeş satırı yenilenir.
2. Mutasyon: özellik denetimleri kaldırılır, workspace.tsx aktarımı yanlış yetkiye bağlanır (cp+cmp ile geri alınır).
3. Yeni Türkçe metin gerekmez; yetkisiz denetim sessizce gizlenir (iade paneliyle aynı kural).

## Out of scope

- Sunucu uçları ve yetki kuralları; müşteri ekranı/kuyruk çift görünüm (tanımsız, ana oturuma bildirilir).

## Dependencies

- None

## Acceptance evidence

1. PosTerminal vitest, tsc --noEmit ve oxlint exit 0; çıktılar `evidence/V1-RMD-858/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
