# V1-RMD-831 - Kalem kaldırma geri alma: satır verisine ürün ve seçenek kimliği

- Task ID: V1-RMD-831
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-826 satış ekranında "Kaldır" için iki adımlı onay getirdi, ama gerçek geri alma yapılamadı: siparişteki satır (`CustomerDisplayLineDto` / istemcide `DisplayLine`) ürün ve seçenek kimliğini taşımıyor, ekran kaldırılan kalemi yeniden kuramıyor. PO kararı: açık bırakma yok. Bu görev satıra `ProductId` ve `ModifierIds` ekler ve kaldırma başarılı olunca "Geri al" sunar.

Davranış: kalem kaldırılınca bildirim satırında "Kalem kaldırıldı" ve "Geri al" görünür. Geri al, aynı ürünü aynı seçeneklerle yeniden ekler ve adedi kaldırılandaki değere getirir. Geri alınamayan durumlar açıkça işaretlenir (geri al görünmez): satırda satır indirimi varsa (indirim yeniden kurulamaz) ya da ürün katalogda artık yoksa. Başka bir eylem ya da sipariş değişince seçenek kapanır. Seçenek adedi birden büyük olan satırda seçenekler tek adetle eklenir; bu sınır RESULT.md'de yazılır ve ayrıca bir ürün kararı gerekirse bildirilir.

Müşteri ekranı aynı sözleşmeyi kullanır ve izin listesi testi (`SnapshotSerializesOnlyTheCustomerAllowlist`) alan sızmasını yasaklar: yeni alanlar sonda ve isteğe bağlıdır, boşken JSON a yazılmaz, anlık görüntü yalnız kasa ucunda (`orders/active`) kimliklerle istenir; müşteri ekranı yolları kimlik görmez.

## Owned surface

- `evidence/V1-RMD-831/**`
- `src/Clients/PosTerminal/src/routes/Cashier.remove-undo.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenContracts.cs (V1-DSP-001) — satır sözleşmesi.
  - src/Host/DualScreen/DualScreenStore.Display.cs (V1-DSP-001) — satır sorgusu.
  - src/Host/DualScreen/CustomerDisplayPaymentProgress.cs (V1-DSP-001) — satır oluşturma.
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs (V1-DSP-001) — yalnız kasa ucu kimlikleri ister.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs (V1-RMD-802) — müşteri ekranı kimlik sızdırmaz.
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs (V1-DSP-001) — sözleşme testi.
  - src/Clients/PosTerminal/src/contracts.ts (V1-CUI-006) — `DisplayLine` alanları.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — Geri al.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.

## Out of scope

- Satır indirimi, satır notu ve seçenek adedinin geri kurulması; vanilla kasa.

## Dependencies

- V1-RMD-826

## Acceptance evidence

1. Gerçek Postgres testi: anlık görüntü satırı ürün kimliğini ve seçenek kimliklerini taşır. PosTerminal vitest tam paket, tsc yeşil: Geri al ürünü seçenekleriyle ve adediyle yeniden ekler; indirimli satırda ve katalogda olmayan üründe görünmez; başka eylemde kapanır.
2. Müşteri ekranı testleri (CustomerDisplayContractTests, DualScreenStoreTests) yeşil.
3. Mutasyon: kimlik alanı, geri ekleme çağrısı, adet düzeltme, indirim koşulu bozulunca test kırılır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
