# V1-RMD-676 - Çevrimdışı uzlaştırma uçunun iki gerileme düzeltmesi

- Task ID: V1-RMD-676
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Goal

V1-RMD-671 ve V1-RMD-674'ün tam çözüm testinde ortaya çıkardığı iki gerileme kapanır. Uzlaştırma ucu, oturumu olmayan bir çağrıya, parti boyutu kontrolünden önce 401 verir (kontrol oturum doğrulamasından sonraya alınır; mimari sözleşme testi yeşile döner). Giriş bütçesinin satır sayısını bekleyen test, garson indirim satırının eklendiğini (yetki limiti göçü) yansıtır.

## Owned surface

- `plan/v1/remediation/V1-RMD-676-offline-reconciliation-regressions.md`
- `evidence/V1-RMD-676/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs - yalnız parti sınırı kontrolünün yeri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OfflineReconciliation/OfflineReconciliationHttpTests.cs - yalnız bütçe satırı beklentisi ve oturumsuz çağrı testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-675-session-lifetime-and-lockout-audit.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kontrolün oturum doğrulamasından sonraya taşınması; testlerin uyarlanması.

## Out of scope

- Composition, Audit EventStore ve ServerEndToEnd testlerinin kırmızıları (bu görevlerle ilgisiz, ayrıca incelenecek).

## Dependencies

- V1-RMD-675

## Acceptance evidence

1. Mimari sözleşme testi ve uzlaştırma Host testleri yeşil; mutasyon kanıtı.

## Handoff

- V1-RMD-677
