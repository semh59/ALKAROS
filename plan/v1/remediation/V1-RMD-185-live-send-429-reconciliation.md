# V1-RMD-185 - Canlı gönderimde 429'ün kuyruğa alınmaması

- Task ID: V1-RMD-185
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin frontend
boyutundaki bulgusunu kapatır: `sendDraft()`'in canlı gönderim yolu,
429'u (hız sınırı) genel 4xx dalına düşürüyordu — bu, gerçek bir
doğrulama reddi için doğru davranış (turda bir şey yanlış, düzelt ve
tekrar gönder), ama bir hız sınırı için yanlış (turda hiçbir şey
yanlış değil, sunucu sadece beklemeyi istiyor). `flushQueue()` zaten
429'u yeniden denenebilir kabul edip turu asla yok saymıyordu; canlı
gönderim yolu artık aynı davranışı gösteriyor — genel 4xx'in aksine,
turu kuyruğa alıp ekrandan kaldırıyor (kuyruk zaten kendi geri
çekilmeli yeniden deneme mekanizmasını taşıyor), garsonu net bir sonraki
adımı olmayan bir hız-sınırı mesajıyla baş başa bırakmıyor.

Kanıt: `tests/E2E/WaiterPwa/specs/` altına geçici bir repro spec'i
(`zz-temp-429-queues-round.spec.js`, commit edilmedi) eklendi:
`/submit-draft`'ı 429 ile yanıtlayacak şekilde `page.route()` kullanıldı,
gerçek "Gönder" akışı denendi. Düzeltme YOKKEN test, ribbon'un kuyruk
sayısını hiç göstermediğini (`#ribbonQueue` hidden kaldı) kanıtladı.
Düzeltme VARKEN aynı test 1/1 passed.

## Owned surface

- `plan/v1/remediation/V1-RMD-185-live-send-429-reconciliation.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/js/offline-queue.js (V1-WTR-053
    sahipliğinde) — `sendDraft()`'e, genel 4xx dalından önce ayrı bir
    429 dalı eklendi; `flushQueue()`'nun zaten yaptığı gibi turu kuyruğa
    alıp ekrandan kaldırıyor.

## Out of scope

- Yok — tek dalın eklenmesi, mevcut genel 4xx/5xx dallarına dokunmadan.

## Dependencies

- V1-WTR-053

## Acceptance evidence

- Geçici repro spec (`zz-temp-429-queues-round.spec.js`, commit
  edilmedi):
  - Düzeltme YOKKEN (`git stash` ile geçici kaldırılarak): FAILED,
    gerçek kanıtla (`#ribbonQueue` hidden kaldı — tur kuyruğa alınmadı).
  - Düzeltme VARKEN: 1/1 passed.
  - Test dosyası doğrulama sonrası silindi; kalıcı kapsamın bir parçası
    değil.
- `node --check js/offline-queue.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  npx playwright test` (`tests/E2E/WaiterPwa`) → 18/18 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
