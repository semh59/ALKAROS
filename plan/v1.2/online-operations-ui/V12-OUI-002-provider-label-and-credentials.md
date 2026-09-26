# V12-OUI-002 - Online kuyrukta platform etiketi ve platform kimlik bilgisi ekranı

- Task ID: V12-OUI-002
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Operasyon ekranı siparişin hangi platformdan geldiğini göstermiyor ve platform bilgileri yalnız ortam
değişkeniyle veriliyor. Kuyrukta platform etiketi ve süzgeci olur. Yetkili yönetici her platformun API bilgilerini
şifreli saklanan bir ekrandan girer (QNB ve Token ekranlarındaki desen).

## Owned surface

- `src/Modules/OnlineOrdering/Credentials/**`
- `tests/Modules/OnlineOrdering/Credentials/**`
- `src/Clients/PosTerminal/src/features/online-platform-credentials/**`
- `src/Host/Experience/OnlineOrdering/OnlinePlatformCredentialEndpoints.cs`
- `evidence/V12-OUI-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Clients/PosTerminal/src/features/online-operations/ ve src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs (V12-OUI-001) — platform etiketi ve süzgeci.
  - src/Clients/PosTerminal/src/routes/ ve src/Clients/PosTerminal/src/shell/ — ekran bağlantısı.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.

## In scope

1. Kuyruk ve sorun listesinde Türkçe platform etiketi; platforma göre süzgeç.
2. Platform başına kimlik bilgisi (AES-256-GCM zarf), maskeli gösterim, değişiklik denetim kaydı; yalnız yönetici izni.
3. Adaptörler bilgiyi bu depodan okur; ortam değişkeni yalnız geriye dönük yedek kalır.

## Out of scope

- Platform bağlantı testi (adaptör görevlerinde).

## Dependencies

- V12-ONL-008

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-OUI-002/` altında.
- `task_scope_tool.py --task-id V12-OUI-002 --diff-base <InProgress commit>` exit 0.
- PosTerminal testleri ve üretim derlemesi yeşil; ekranda ham İngilizce değer görünmez.

## Handoff

- None
