# V1-RMD-573 - Dockerfile apk sürüm sabitlerini güncelleme

- Task ID: V1-RMD-573
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

`api` ve `connector` imajları yeniden derlenebilir: Alpine deposunda artık bulunmayan openssl 3.5.8-r0 sabitleri, depodaki güncel yama sürümü 3.5.9-r0 ile değiştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-573-dockerfile-apk-pin-refresh.md`
- `evidence/V1-RMD-573/**`
- `deploy/docker/Dockerfile`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Dockerfile'daki `libcrypto3`, `libssl3` ve `openssl` sabitlerinin 3.5.9-r0 yapılması; `api` ve `connector` hedeflerinin derlenmesi.

## Out of scope

- Taban imaj özetlerini (digest) değiştirmek; sabit mantığını kaldırmak.

## Dependencies

- None

## Acceptance evidence

1. `docker compose build api migrate connector` hatasız biter.
2. Çıktılar `evidence/V1-RMD-573/` altındadır.

## Handoff

- None
