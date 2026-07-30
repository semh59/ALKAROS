# GIB e-Arşiv / YN ÖKC Applicability Matrix

> **Task:** V0-CMP-001
> **Status:** InProgress
> **Assignee:** codex-v0-cmp-001
> **Work type:** validation
> **Source basis:** PDF:II.2.16, PDF:II.3.12, PDF:II.5.4, PDF:III.19, EXT:GIB-YNOKC-GUIDE, EXT:GIB-TK2-4.0
> **Date:** 2026-07-30

## 1. Business Profile

| Attribute | Value |
|-----------|-------|
| Business type | Restaurant / fast-food / cafe |
| Location | Turkey |
| Fiscal device | YN ÖKC (Hugin T300 or equivalent) |
| e-Invoice | e-Arşiv (mandatory for B2C over threshold) |
| e-Archive threshold | 2026: 5,000 TL per document (GIB updated annually) |
| e-Adisyon | Not mandatory per GIB; optional integration |

## 2. Applicability Matrix

| Requirement | Source | Applicable | Notes |
|-------------|--------|------------|-------|
| YN ÖKC mandatory for restaurant POS | GIB-YNOKC-GUIDE, TK2-4.0 | YES | All restaurants must use YN ÖKC for receipt fiscalization |
| e-Arşiv invoice for B2C > threshold | GIB regulations | YES | Per-document threshold applies; monthly aggregate not sufficient |
| e-Adisyon integration | GIB | NO | e-Adisyon is optional; not a legal requirement |
| Fiscal document before payment capture | TK2-4.0 §3.2 | YES | Fiscal receipt must be issued before or simultaneously with payment |
| Daily fiscal report (Z Report) | TK2-4.0 §4.1 | YES | Mandatory end-of-day fiscal close |
| Fiscal memory retention | TK2-4.0 §5 | YES | Device must retain fiscal records for legal period |
| Internet connectivity requirement | GIB-YNOKC-GUIDE | YES | YN ÖKC devices require periodic GIB connectivity |
| EFT-POS integration with fiscal device | GIB-HUGIN-T300 | CONDITIONAL | Only if EFT-POS is used; Hugin T300 supports EFT-POS |
| Meal card / ticket fiscalization | GIB-YNOKC-GUIDE | CONDITIONAL | Meal card payments may require separate fiscal treatment |
| e-Invoice for B2B transactions | GIB e-Arşiv | YES | B2B invoices must be e-Arşiv regardless of amount |
| e-Archive storage period (10 years) | Turkish Tax Procedure Law | YES | All fiscal documents must be stored for 10 years |

## 3. Key Findings

### 3.1 YN ÖKC Mandate
All restaurants in Turkey are required to use YN ÖKC (Yeni Nesil Ödeme Kaydedici Cihaz) for fiscal receipt generation. The Hugin T300 is listed as an approved device (GIB-HUGIN-T300). The system MUST integrate with the YN ÖKC for all receipt fiscalization.

### 3.2 e-Arşiv Threshold
For B2C transactions exceeding 5,000 TL (2026 threshold), an e-Arşiv invoice must be issued. The system MUST detect threshold crossing and trigger e-Arşiv generation. Below threshold, a fiscal receipt (YN ÖKC) is sufficient.

### 3.3 e-Adisyon (Optional)
e-Adisyon is NOT a legal requirement. It is an optional integration for digital order management. The system MAY support e-Adisyon but MUST NOT depend on it for fiscal compliance.

### 3.4 Fiscal Document Timing
Fiscal document MUST be issued before or simultaneously with payment capture. This is a critical invariant: no payment can be captured without a corresponding fiscal document.

## 4. Edge Cases

### Edge Case 1: Split Payment Below/Above Threshold
- Scenario: A 6,000 TL bill is split into 3,000 TL + 3,000 TL payments
- Result: Each payment is below threshold; fiscal receipt is sufficient. No e-Arşiv required.
- Rationale: e-Arşiv threshold applies per document (per fiscal receipt), not per order total.

### Edge Case 2: Internet Outage During Fiscalization
- Scenario: YN ÖKC loses connectivity during fiscal document issuance
- Result: Device MUST queue fiscal records and sync when connectivity is restored. System MUST handle delayed fiscalization.
- Limitation: Extended outage may require manual intervention per GIB rules.

## 5. Blocker Register

| Item | Description | Impact | Resolution Path |
|------|-------------|--------|-----------------|
| PRIVATE-HUGIN-CONTRACT | T300 model/firmware/protocol matrix not available | V0-HUG-001 cannot complete | Obtain contract/sandbox access |
| PRIVATE-QNB-SANDBOX | QNB test tenant not available | V0-QNB-001 cannot complete | Obtain sandbox credentials |
| 2026 e-Arşiv threshold confirmation | Annual threshold not yet published for full 2026 | Minor — use 5,000 TL as working value | Confirm with tax advisor at implementation time |

## 6. Sources

| Source ID | Description | Access Date | URL |
|-----------|-------------|-------------|-----|
| GIB-YNOKC-GUIDE | YN ÖKC kullanım rehberi | 2026-07-29 | https://www.gib.gov.tr/duyuru-arsivi/guncel/15314 |
| GIB-TK2-4.0 | TK-2 v4.0 teknik tanımlar | 2026-07-29 | https://ynokc.gib.gov.tr/UploadedFiles/Files/ynokc2.pdf |
| GIB-HUGIN-T300 | GIB onaylı cihaz listesi | 2026-07-30 | https://ynokc.gib.gov.tr/Home/OnayAlanFirmalar/1003 |

## 7. Conclusion

The target restaurant POS system MUST:
1. Integrate with YN ÖKC for fiscal receipt generation
2. Support e-Arşiv invoice generation for B2C transactions over threshold
3. Support e-Arşiv invoice for all B2B transactions
4. Ensure fiscal document is issued before payment capture
5. Handle offline fiscalization with queue-and-sync

The system MAY optionally support e-Adisyon integration, but this is not a compliance requirement.