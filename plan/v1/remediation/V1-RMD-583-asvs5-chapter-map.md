# V1-RMD-583 - ASVS 5.0 bölüm haritası

- Task ID: V1-RMD-583
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Onaylı güvenlik temel kaydı (V0-SEC-001) "ASVS 5.0.0" diyor ama kategori başına yazdığı aralıkların (ör. Authentication 2.1-2.19, Access Control 4.1-4.11) ASVS 5.0.0'da karşılığı yok: bunlar 4.0 numaralarıdır; 5.0.0'da kimlik biçimi `v5.0.0-6.2.1` ve bölümler farklıdır (Authentication V6, Authorization V8, Encoding V1 ...). Bağımsız değerlendirme (V20-SEC-001) yanlış kapsam çıkarmasın diye resmî 5.0.0 bölümlerine eşleme belgesi yazılır. Onaylı kaydın kararına (seviye, tehdit modeli) dokunulmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-583-asvs5-chapter-map.md`
- `evidence/V1-RMD-583/**`
- `docs/security/asvs-5-chapter-map.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v2.0/security-compliance/V20-SEC-001-independent-security-assessment.md - yalnız haritaya işaret eden satır

## In scope

- Kategori başına doğrulanmış ASVS 5.0.0 bölüm ve kesim başlıkları.
- Kimlik biçimi ve seviye (L1/L2/L3) tanımının kaynağı.
- Açık kalanlar: seviye kararı (L3 onaylı) ve imza satırları.

## Out of scope

- Seviye veya tehdit modeli kararını değiştirmek; imza atmak.

## Dependencies

- V0-SEC-001

## Acceptance evidence

1. Her bölüm başlığı OWASP/ASVS v5.0.0 etiketinin kendi dosyalarından doğrulanmıştır (kaynak adresleri belgede).

## Handoff

- None
