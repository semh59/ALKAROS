# V1-RMD-188 - "Geri al" mesajı yanlışlıkla "masa değişti" diyordu

- Task ID: V1-RMD-188
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin bulgusunu
kapatır: bir kalemi "çıkar" edip "Geri al" toast'ını gördükten sonra,
o toast beş saniye içinde kullanılamazsa (masa değişti YA DA aynı
masada tur gönderildi/temizlendi), eski kod ikisini de aynı tek koşulla
(`state.table.id !== ownerTableId || state.draftEpoch !== epoch`)
yakalayıp her zaman "masa değişti" diyordu. Ama `draftEpoch`, aynı
dosyadaki kendi yorumunun da söylediği gibi, iki farklı sebep için
ilerliyor: masa değişmesi DEĞİL, turun kendisinin gönderilmesi/
temizlenmesi de `draftEpoch`'u ilerletiyor (`offline-queue.js`'in
`removeSentDraftLines`'ı, `pending-orders.js`'in kendi dalı). Garson
hâlâ aynı masadayken "masa değişti" denmesi yanlıştı.

Kanıt: `tests/E2E/WaiterPwa/specs/` altına geçici bir repro spec'i
(`zz-temp-undo-toast-epoch.spec.js`, commit edilmedi) eklendi: aynı
masada iki ayrı kalem eklendi, biri sıfıra indirilip kaldırıldı ("Geri
al" toast'ı çıktı), masa DEĞİŞMEDEN diğer kalem gönderildi (draftEpoch
ilerledi), sonra "Geri al" tıklandı. Düzeltme YOKKEN test, mesajın
gerçekten "E2E Köfte geri alınamadı, masa değişti." dediğini (aynı
masadayken) kanıtladı. Düzeltme VARKEN aynı test, mesajın "tur değişti"
dediğini doğruladı.

## Owned surface

- `plan/v1/remediation/V1-RMD-188-undo-toast-wrong-table-changed-message.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010
    sahipliğinde) — undo callback'i, masa değişikliği ve tur değişikliği
    kontrollerini ayırıp her biri için doğru mesajı veriyor.

## Out of scope

- Yok — tek callback'in iki ayrı, doğru mesaja bölünmesi.

## Dependencies

- V1-WTR-010

## Acceptance evidence

- Geçici repro spec (`zz-temp-undo-toast-epoch.spec.js`, commit
  edilmedi):
  - Düzeltme YOKKEN (`git stash` ile geçici kaldırılarak): FAILED,
    gerçek kanıtla (mesaj aynı masadayken "masa değişti." dedi).
  - Düzeltme VARKEN: 1/1 passed ("tur değişti." doğru mesajı).
  - Test dosyası doğrulama sonrası silindi; kalıcı kapsamın bir parçası
    değil.
- `node --check waiter-app.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  npx playwright test` (`tests/E2E/WaiterPwa`) → 18/18 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
