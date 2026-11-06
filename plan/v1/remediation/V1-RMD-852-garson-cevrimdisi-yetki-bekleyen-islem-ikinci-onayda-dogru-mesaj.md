# V1-RMD-852 - Garson çevrimdışı iptal/ikram: kuyrukta bekleyen işlemin ikinci onayı yanlış uyarı vermez

- Task ID: V1-RMD-852
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (V1-RMD-850 incelemesi): `authorizeOffline` (`sheets/void-comp.js`) yetki denetimini kuyruk tekrar denetiminden önce yapıyor; aynı kalem için çevrimdışı onay `maxCount` hakkını doldurduktan sonra ikinci onayda garson, kendi kaydı kuyrukta beklerken "çevrimdışı işlem hakkınız doldu" uyarısı alıyor. Kuyrukta aynı anahtarlı işlem zaten bekliyorsa yetki denetimi atlanır, kayıt bir kez tutulur ve garsona "işlem zaten kuyrukta" Türkçe bilgisi gösterilir; `queueOfflineAuthorizedAction` içindeki (V1-RMD-850) tekrar koruması tek yerde kalır.

## Owned surface

- `evidence/V1-RMD-852/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/WaiterPwa/wwwroot/js/sheets/void-comp.js — `authorizeOffline` sırası ve metin.
  - src/Clients/WaiterPwa/wwwroot/js/offline-queue.js — tekrar denetiminin tek yerde toplanması.
  - tests/Clients/WaiterPwa/Frontend/test_offline_authorized_void_comp.py — `maxCount: 1` ile ikinci onay testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı: `maxCount: 1` bütçesinde aynı işlemin ikinci onayı "hakkınız doldu" uyarısı veriyor, "zaten kuyrukta" vermiyor.
2. Düzeltme ve mutasyon (sıra geri çevrilince test düşer; cp+cmp).

## Out of scope

- Sunucu uzlaştırma ucu, bütçe hesabı (`state.js`).

## Dependencies

- None

## Acceptance evidence

1. WaiterPwa Frontend ve StaticApps koşuları exit 0; çıktı `evidence/V1-RMD-852/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
