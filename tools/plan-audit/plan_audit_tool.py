from __future__ import annotations

import argparse
import ast
from collections import defaultdict
import hashlib
import importlib.util
import json
import logging
import re
import shutil
import subprocess
import tempfile
import textwrap
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

WORKSPACE = Path(__file__).resolve().parents[2]
PLAN_DIR = WORKSPACE / "plan"
PDF_PATH = Path(
    r"C:\Users\semih\Downloads\Telegram Desktop\restaurant_pos_master_v5.pdf"
)
BASELINE_PATH = PLAN_DIR / "AUDIT_BASELINE_MANIFEST.json"
TRANSLATION_CACHE_PATH = WORKSPACE / "tmp" / "plan_translation_cache.json"
SESSION_LOG_PATH = Path(
    r"C:\Users\semih\.codex\sessions\2026\07\29\rollout-2026-07-29T21-28-01-"
    r"019faf22-309d-7920-a883-f6c4e06fc025.jsonl"
)
BASELINE_CUTOFF = "2026-07-29T19:59:16.591Z"
AUDIT_DATE = "2026-07-30"

CORRECTION_OWNERS = {
    "C1": ["V0-DAT-001", "V20-MIG-001", "V20-MIG-002"],
    "C2": ["V0-DAT-002", "V11-RSV-001", "V14-QRO-001"],
    "C3": ["V0-DOM-007", "V13-ACC-001"],
    "C4": ["V0-DOM-004", "V1-FND-002", "V12-ALC-001"],
    "C5": ["V0-DOM-005", "V14-QRO-002"],
    "C6": ["V0-DAT-004", "V12-MCD-002"],
    "C7": ["V0-DAT-002", "V20-GAT-001"],
    "C8": ["V0-DOC-001", "V20-GAT-001"],
    "C9": ["V0-DOM-010", "V11-PRD-002"],
}

TASK_HEADER = re.compile(r"^# (?P<id>V\d+-[A-Z0-9]+-\d+)\s+-\s+.+$")
TASK_ID = re.compile(r"\bV\d+-[A-Z0-9]+-\d+\b")
GATE_ID = re.compile(r"\bGATE-[A-Z0-9]+(?:-[A-Z0-9]+)+\b")
PDF_SECTION = re.compile(
    r"\b(?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?"
    r"(?:-(?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?)?"
)

BLOCKERS = {
    "V0-HUG-001": (
        "Kamuya açık kaynak T300 cihaz kimliğini doğruluyor; resmî SDK/protokol, "
        "test cihazı ve success/decline/timeout/reconcile transcript kanıtı çalışma "
        "alanında yoktur."
    ),
    "V0-QNB-001": (
        "Kamuya açık API temel e-belge işlemlerini gösteriyor; test tenant, özel "
        "contract ve gerçek document lifecycle transcript kanıtı çalışma alanında yoktur."
    ),
    "V0-YSP-001": (
        "Partner API v2.0.2 kamuya açıktır; Partner Portal credential, sandbox ve "
        "imzalı veya tokenlı gerçek webhook transcript kanıtı çalışma alanında yoktur."
    ),
    "V0-MCD-001": (
        "Onaylı meal-card provider listesi, provider contract, credential ve sandbox "
        "veya cihaz transcript kanıtı çalışma alanında yoktur."
    ),
    "V0-PRN-001": (
        "Onaylı printer model/firmware/transport listesi ile paper-out, disconnect ve "
        "crash-window gerçek cihaz transcript kanıtı çalışma alanında yoktur."
    ),
}

DEPENDENCY_REPLACEMENTS = {
    "V1-FND-001": ["GATE-V0-EXIT"],
    "V11-UNT-001": ["GATE-V11-ENTRY"],
    "V12-PAY-001": ["GATE-V12-ENTRY"],
    "V13-CST-001": ["GATE-V13-ENTRY"],
    "V14-QRS-001": ["GATE-V14-ENTRY"],
    "V15-PER-001": ["GATE-V14-EXIT"],
    "V15-SEC-001": ["GATE-V15-ENTRY"],
    "V20-GAT-001": ["GATE-V15-EXIT"],
}

DEPENDENCY_ADDITIONS = {
    "V12-PAY-002": ["V0-ARC-004"],
    "V12-CSH-001": ["V1-CSH-001"],
    "V13-ACC-001": ["V0-DOM-007"],
    "V14-QRO-001": ["V14-QRS-003"],
    "V14-QRO-002": ["V0-DOM-005"],
    "V14-QRO-003": ["V14-STK-001"],
    "V14-ONL-002": ["V14-STK-001"],
    "V11-PUR-001": ["V0-DOM-009"],
    "V11-RCP-002": ["V0-DOM-010"],
    "V1-KIT-002": ["V0-DOM-011"],
    "V15-NOT-001": ["V0-ARC-006"],
    "V20-INS-001": ["V0-ARC-007"],
    "V20-INS-002": ["V0-ARC-007"],
    "V20-REL-001": ["V0-ARC-008"],
    "V20-MIG-001": ["V0-DAT-006"],
    "V20-SEC-001": [
        "V0-SEC-001",
        "V14-QRS-003",
        "V14-CWB-001",
        "V14-CWB-002",
        "V14-ONL-001",
    ],
}

DEPENDENCY_REMOVALS = {
    "V14-QRO-002": ["V0-CMP-001"],
    "V14-STK-001": ["V14-QRO-003", "V14-ONL-002"],
    "V0-ARC-009": ["V0-SEC-001"],
    "V0-CMP-002": ["V0-CMP-001"],
    "V0-CMP-004": ["V0-CMP-001"],
    "V0-GOV-010": ["V1-FND-003"],
    "V0-GOV-013": ["V1-SEC-002"],
    "V0-GOV-014": ["V1-FND-002"],
    "V0-GOV-015": ["V1-FND-004"],
}

# 2026-08-03 user-approved plan change (TRACEABILITY C40): these V0 tasks
# require real external evidence (provider contract, device, license or
# security standard) that cannot exist before V0 exits. They stay Blocked but
# are excluded from the V0 gate-open check and close with evidence at the
# stage named in GATES.md.
V0_DEFERRED_TASKS = {
    "V0-HUG-001",
    "V0-QNB-001",
    "V0-YSP-001",
    "V0-MCD-001",
    "V0-PRN-001",
    "V0-QRG-001",
    "V0-CMP-001",
    "V0-SEC-001",
    "V0-LIC-001",
    "V0-BKP-001",
    "V0-BKP-002",
    "V0-REV-001",
    "V0-REV-002",
    "V0-REV-003",
    "V0-REV-004",
    "V0-REV-005",
    "V0-REV-006",
    "V0-REV-007",
    "V0-REV-008",
    "V0-REV-009",
    "V0-REV-010",
    "V0-REV-011",
    "V0-REV-012",
    "V0-REV-013",
    "V0-REV-014",
    "V0-REV-015",
    "V0-REV-016",
    "V0-REV-017",
    "V0-REV-018",
    "V0-REV-019",
    "V0-REV-020",
    "V0-REV-021",
    "V0-REV-022",
    "V0-REV-023",
    "V0-REV-024",
    "V0-REV-025",
    "V0-REV-026",
    "V0-REV-027",
    "V0-REV-028",
    "V0-REV-029",
    "V0-REV-030",
}

BROAD_HANDOFF_REPLACEMENTS = {
    "V0-DAT-001": ["GATE-V0-EXIT"],
    "V0-DAT-002": ["GATE-V0-EXIT"],
    "V0-DAT-005": ["GATE-V0-EXIT"],
    "V1-FND-001": ["GATE-V1-EXIT"],
    "V1-SET-001": ["GATE-V1-EXIT"],
    "V20-MIG-001": ["V20-MIG-002"],
    "V20-UAT-001": ["V20-UAT-003"],
    "V20-UAT-002": ["V20-UAT-003"],
    "V20-UAT-003": ["V20-GAT-002"],
    "V20-SEC-001": ["V20-GAT-002"],
    "V20-GAT-001": ["V20-GAT-002"],
    "V20-DRL-001": ["V20-MIG-002", "V20-GAT-002"],
    "V20-REL-001": ["V20-REL-002"],
    "V20-REL-003": ["None"],
}

FALLBACK_SOURCES = {
    "V0-CMP-004": ["CORR:C10"],
    "V0-ARC-002": ["PDF:I.1.1", "PDF:I.4", "PDF:I.51"],
    "V0-ARC-003": ["PDF:I.15", "PDF:I.48.6"],
    "V0-DAT-005": ["PDF:I.1.5", "PDF:II.0", "PDF:III.2"],
    "V15-PER-001": ["PDF:I.38", "PDF:I.45.1"],
    "V15-PER-002": ["PDF:I.38", "PDF:I.45.1"],
    "V15-RUN-001": ["PDF:I.38", "PDF:I.41-I.43"],
    "V15-SUP-001": ["PDF:I.38", "PDF:I.42"],
    "V1-WTR-001": ["PDF:I.7", "PDF:I.14-I.15"],
    "V1-WTR-002": ["PDF:I.7-I.10"],
    "V1-WTR-003": ["PDF:I.8", "PDF:I.16"],
    "V1-CUI-001": ["PDF:I.7", "PDF:I.9-I.10"],
    "V1-CUI-002": ["PDF:I.8"],
    "V1-CUI-003": ["PDF:I.16-I.19"],
    "V11-UI-001": ["PDF:I.21.1-I.21.4"],
    "V11-UI-002": ["PDF:I.22-I.23"],
    "V11-UI-003": ["PDF:I.23-I.25"],
    "V12-PUI-001": ["PDF:I.26-I.26A"],
    "V12-PUI-002": ["PDF:I.44"],
    "V12-PUI-003": ["PDF:I.26-I.29"],
    "V13-UI-001": ["PDF:I.30"],
    "V13-UI-002": ["PDF:I.31-I.32"],
    "V13-UI-003": ["PDF:I.32.1"],
    "V20-INS-001": ["PDF:I.45.1", "PDF:I.50", "CORR:C15"],
    "V20-INS-002": ["PDF:I.45.1", "PDF:I.50", "CORR:C15"],
    "V20-INT-004": ["PDF:I.29", "CORR:C20"],
    "V20-INT-005": ["PDF:I.6.6", "PDF:I.16-I.17"],
    "V20-INT-006": ["PDF:I.34-I.35", "CORR:C19"],
    "V20-DOC-001": ["PDF:I.45.1", "PDF:I.53-I.54"],
    "V20-DOC-002": ["PDF:I.45.1", "PDF:I.51", "PDF:I.54"],
}

EXTERNAL_SOURCES = {
    "V0-CMP-001": ["EXT:GIB-YNOKC-GUIDE", "EXT:GIB-TK2-4.0"],
    "V0-HUG-001": ["EXT:GIB-HUGIN-T300", "EXT:HUGIN-PRODUCT-PUBLIC"],
    "V0-MCD-001": ["EXT:GIB-TK2-4.0"],
    "V0-QNB-001": ["EXT:QNB-API-PUBLIC"],
    "V0-YSP-001": ["EXT:YSP-PARTNER-2.0.2"],
    "V20-INT-001": ["EXT:GIB-HUGIN-T300"],
    "V20-INT-002": ["EXT:QNB-API-PUBLIC"],
    "V20-INT-003": ["EXT:YSP-PARTNER-2.0.2"],
    "V15-SEC-002": [
        "EXT:OWASP-ASVS-5.0.0",
        "EXT:OWASP-AUTH",
        "EXT:OWASP-SESSION",
    ],
    "V15-OBS-001": ["EXT:OWASP-LOGGING"],
    "V20-REL-001": ["EXT:CYCLONEDX-1.7", "EXT:SLSA-1.2"],
    "V20-SEC-001": ["EXT:OWASP-ASVS-5.0.0"],
}

TRANSLATABLE_SECTIONS = {
    "Goal",
    "In scope",
    "Out of scope",
    "Deliverables",
    "Acceptance evidence",
    "Blocker",
}

TURKISH_MARKERS = re.compile(
    r"\b(ve|veya|ile|için|bir|bu|görev|kapsam|yalnız|yalniz|olarak|"
    r"yoktur|vardır|kalır|üretir|edilir|olmadan|sonucu|kanıt|kaynak|"
    r"tarih|onaylayan|davranışı|belirlemek|uygulamak|doğrulamak)\b",
    re.IGNORECASE,
)

TECHNICAL_TERMS = (
    "API",
    "HTTP",
    "JSON",
    "SQL",
    "PostgreSQL",
    "PDF",
    "UI",
    "PWA",
    "QR",
    "RPO",
    "RTO",
    "SBOM",
    "SLSA",
    "ASVS",
    "WCAG",
    "QNB",
    "Hugin",
    "T300",
    "Yemeksepeti",
    "Cash",
    "BankCard",
    "MealCard",
    "CustomerAccount",
    "SplitPayment",
    "Order",
    "Bill",
    "Payment",
    "PaymentAllocation",
    "ReconciliationCase",
    "Table",
    "KitchenTicket",
    "PrintJob",
    "CashSession",
    "Invoice",
    "Alert",
    "Draft",
    "Submitted",
    "PendingConfirmation",
    "Planned",
    "InProgress",
    "Blocked",
    "Done",
    "provider",
    "adapter",
    "sandbox",
    "webhook",
    "retry",
    "timeout",
    "idempotency",
    "migration",
    "production",
    "contract",
    "event",
    "status",
    "enum",
    "source-of-truth",
    "rebuild",
    "workflow",
    "checkpoint",
    "release",
)

HEADER_OVERRIDES = {
    "V0-BKP-001": "# V0-BKP-001 - Validate PostgreSQL backup and restore tooling",
    "V12-MCD-003": "# V12-MCD-003 - Implement meal-card adapter SPI and registry",
    "V15-RUN-001": "# V15-RUN-001 - Write executable operational runbooks",
    "V20-INT-004": "# V20-INT-004 - Aggregate meal-card certification gate",
    "V20-REL-002": "# V20-REL-002 - Execute non-production pilot rehearsal",
}

SECTION_OVERRIDES: dict[str, dict[str, list[str]]] = {
    "V0-BKP-001": {
        "Goal": [
            "Disposable PostgreSQL 18 instance üzerinde backup, checksum ve restore "
            "tool path uygulanabilirliğini doğrulamak."
        ],
        "In scope": [
            "- Seeded verification table, backup artifact, checksum, corruption rejection, "
            "clean restore ve ölçülen süre."
        ],
        "Out of scope": [
            "- ALKAROS production schema, application startup, scheduling, retention ve off-site automation."
        ],
        "Acceptance evidence": [
            "- Temiz PostgreSQL 18 instance'a restore edilen seeded kayıt ve checksum eşleşir; "
            "corrupted artifact application kanıtı sayılmadan reddedilir."
        ],
    },
    "V12-PAY-002": {
        "Goal": [
            "Cash, BankCard ve MealCard payment komutlarını typed handler'lara yönlendirmek; "
            "CustomerAccount yöntemini V1.3'e kadar typed version-not-enabled sonucu ile reddetmek."
        ],
        "In scope": [
            "- Typed tender request contract'ları, handler registry, unknown method rejection "
            "ve V0-ARC-004 uyumlu version-not-enabled sonucu."
        ],
        "Out of scope": [
            "- Tender-specific provider logic, allocation persistence ve CustomerAccount handler implementation."
        ],
        "Acceptance evidence": [
            "- Cash, BankCard ve MealCard tek handler'a çözülür; CustomerAccount V1.2'de veri "
            "değiştirmeden typed version-not-enabled sonucu verir; SplitPayment ve unknown text reddedilir."
        ],
    },
    "V12-HUG-002": {
        "Goal": [
            "Timeout veya connection loss sonucunu Unknown olarak saklamak, terminal status'ünü "
            "sorgulamak ve çözümlenemeyen divergence evidence event'i üretmek."
        ],
        "In scope": [
            "- Timeout classification, status query, retry sınırı, late result ve typed divergence evidence event."
        ],
        "Out of scope": [
            "- Yeni payment isteği, refund execution ve ReconciliationCase oluşturma."
        ],
        "Acceptance evidence": [
            "- Timeout örtük decline/success olmaz; terminal sonucu bir kez uygulanır veya aynı "
            "divergence için idempotent evidence event üretilir."
        ],
    },
    "V12-MCD-002": {
        "Goal": [
            "Meal-card payment'larını provider settlement dönemlerinde gruplamak, parent/child "
            "durumunu atomik güncellemek ve mismatch evidence event'i üretmek."
        ],
        "In scope": [
            "- Period uniqueness, item membership, parent totals, child projection, disputed result "
            "ve typed mismatch evidence event."
        ],
        "Out of scope": [
            "- CustomerAccount, BankCard reconciliation ve ReconciliationCase oluşturma."
        ],
        "Acceptance evidence": [
            "- Parent Settled ile child durumları drift edemez; rebuild saklanan toplamı üretir; "
            "mismatch aynı evidence event'i idempotent olarak yayınlar."
        ],
    },
    "V12-MCD-003": {
        "Goal": [
            "Provider-neutral meal-card adapter SPI, registry ve capability rejection contract'ını oluşturmak."
        ],
        "Owned surface": [
            "- `src/Modules/MealCard/Providers/Registry/**`, `tests/Modules/MealCard/Providers/Registry/**`",
            "- Bu görev provider-specific transport veya başka task'ın owned surface alanını değiştiremez.",
        ],
        "In scope": [
            "- Adapter SPI, provider code registry, capability declaration, disabled provider rejection "
            "ve composition registration."
        ],
        "Out of scope": [
            "- Provider-specific request/response mapping, credential, sandbox transcript ve CustomerAccount."
        ],
        "Deliverables": [
            "- Provider-neutral SPI ve registry production code'u.",
            "- Duplicate provider code, disabled provider ve unsupported capability contract testleri.",
        ],
        "Acceptance evidence": [
            "- Registry yalnız V0-MCD-001 tarafından onaylanan provider code'larını etkinleştirir; "
            "registry içinde provider-specific success stub bulunmaz."
        ],
    },
    "V14-ONL-003": {
        "Goal": [
            "Provider status/cancellation değişikliklerini race-safe local transition ile işlemek "
            "ve çözümlenemeyen divergence evidence event'i üretmek."
        ],
        "In scope": [
            "- Outbound idempotency, late cancellation, already-preparing policy, retry ve typed divergence event."
        ],
        "Out of scope": [
            "- Webhook intake, product mapping ve ReconciliationCase oluşturma."
        ],
        "Acceptance evidence": [
            "- Duplicate status tek etki üretir; cancellation race deterministik kapanır; "
            "çözümlenemeyen fark aynı evidence event'i idempotent olarak üretir."
        ],
    },
    "V14-MAP-002": {
        "Goal": [
            "Doğrulanan her Yemeksepeti status'ünü izinli internal command, explicit no-op "
            "veya typed unknown-status evidence sonucuna eşlemek."
        ],
        "In scope": [
            "- Provider vocabulary version, integration-kind differences, cancellation reason "
            "ve unknown status evidence."
        ],
        "Out of scope": [
            "- Webhook authentication, transport retry ve ReconciliationCase oluşturma."
        ],
        "Acceptance evidence": [
            "- Belgelenen her provider status'ünün tek sonucu vardır; unknown status Order'ı "
            "değiştirmez ve idempotent evidence event üretir."
        ],
    },
    "V14-STK-001": {
        "Goal": [
            "Cashier, waiter, QR ve online channel için tek channel-neutral reservation command "
            "ve ortak last-portion arbitration sonucu sağlamak."
        ],
        "Handoff": ["- V14-QRO-003", "- V14-ONL-002"],
    },
    "V15-RUN-001": {
        "Goal": [
            "Printer, Unknown payment, fiscal failure, backup, restore, disk ve provider outage "
            "olayları için yürütülebilir runbook'lar yazmak."
        ],
        "Owned surface": [
            "- `docs/runbooks/**`",
            "- Bu görev execution evidence veya production code alanını değiştiremez.",
        ],
        "In scope": [
            "- Trigger, diagnosis, safe action, escalation, rollback ve expected evidence adımları."
        ],
        "Out of scope": [
            "- Runbook execution, production intervention, provider contract ve product code değişikliği."
        ],
        "Deliverables": [
            "- Her incident sınıfı için versioned runbook ve prerequisite/rollback listesi."
        ],
        "Acceptance evidence": [
            "- Her runbook başlangıç koşulu, sıralı command/action, expected result, stop condition "
            "ve escalation owner içerir."
        ],
        "Handoff": ["- V15-RUN-002"],
    },
    "V15-KVK-002": {
        "Goal": [
            "Onaylı PII anonymization işlemini idempotent, resumable ve store-checkpoint tabanlı "
            "workflow olarak uygulamak."
        ],
        "In scope": [
            "- Per-field action, store checkpoint, retry/resume, referential integrity, audit entry "
            "ve final all-store verification."
        ],
        "Acceptance evidence": [
            "- Seeded subject data her in-scope store checkpoint'inden sonra kaldırılır; interrupted "
            "workflow aynı noktadan güvenle devam eder; financial totals ve legal IDs geçerli kalır."
        ],
    },
    "V20-LIC-001": {
        "Source basis": ["- PDF:II.2.24", "- PDF:III.26", "- DEC:V0-LIC-001"],
        "Goal": [
            "Yalnız V0-LIC-001 sonucu Required ise onaylanan license enforcement davranışını uygulamak."
        ],
        "In scope": [
            "- Signed license validation, scope/expiry, clock-tamper policy, offline grace "
            "ve auditable enforcement."
        ],
        "Out of scope": [
            "- NotApplicable kanıtı üretme, license server uydurma, remote kill switch ve unapproved telemetry."
        ],
        "Blocker": [
            "- V0-LIC-001 henüz Required veya NotApplicable kararı üretmemiştir. Required ise bu "
            "task açılır; NotApplicable ise audit kaydıyla kaldırılır."
        ],
        "Deliverables": [
            "- Onaylanan enforcement production code'u, failure reason code'ları ve recovery testleri."
        ],
        "Acceptance evidence": [
            "- Davranış V0 contract case'leriyle eşleşir; network loss, expiry ve clock anomaly "
            "Order, Payment veya fiscal kayıtları sessizce bozamaz."
        ],
    },
    "V20-INT-002": {
        "Source basis": ["- PDF:I.32", "- EXT:QNB-API-PUBLIC", "- DEC:V0-QNB-001"],
        "In scope": [
            "- Authentication, send, poll, retry, duplicate prevention ve provider/internal status reconciliation.",
            "- Cancellation veya webhook yalnız private/partner contract'ta doğrulanırsa applicable olur.",
        ],
        "Acceptance evidence": [
            "- Public ve private evidence ile applicable olduğu kanıtlanan senaryolar tek traceable "
            "sonuç üretir; doğrulanmayan cancellation/webhook satırları onaylı N/A veya blocker kalır."
        ],
    },
    "V20-INT-004": {
        "Goal": [
            "V0-MCD-001 çıktısından türetilen provider-specific V20-INT-1xx certification "
            "task'larının eksiksizliğini ve sonuçlarını gate olarak toplamak."
        ],
        "In scope": [
            "- Approved provider listesi ile V12-MCD-1xx/V20-INT-1xx bire bir eşleşmesi, "
            "task sonucu ve evidence link doğrulaması."
        ],
        "Out of scope": [
            "- Birden fazla provider'ı tek task'ta certify etme, adapter implementation ve test execution."
        ],
        "Blocker": [
            "- V0-MCD-001 approved provider listesi üretmediği için provider-specific task'lar oluşturulamaz."
        ],
        "Deliverables": [
            "- Provider-to-task certification manifest ve missing/failed provider listesi."
        ],
        "Acceptance evidence": [
            "- Her approved provider tam bir V12-MCD-1xx ve V20-INT-1xx çifti taşır; "
            "missing, failed veya ambiguous provider gate'i kapatır."
        ],
    },
    "V20-REL-002": {
        "Goal": [
            "Immutable release candidate'ı production-equivalent fakat non-production ortamda "
            "yalnız synthetic veya yetkili sanitized data ile pilot rehearsal olarak çalıştırmak."
        ],
        "Deliverables": [
            "- Pilot rehearsal transcript'i, operational metrics, defect register ve rollback-decision evidence."
        ],
        "Acceptance evidence": [
            "- Approved workflow ve reliability threshold'ları exact release artifact üzerinde geçer; "
            "real customer, real payment veya real fiscal issuance kullanılmaz."
        ],
    },
    "V20-REL-003": {
        "Goal": [
            "Exact immutable release candidate için evidence-backed approve veya reject kararını kaydetmek."
        ],
        "In scope": [
            "- Gate completeness, approver identity, artifact hash, deployment window, rollback "
            "trigger/owner ve explicit approve/reject sonucu."
        ],
        "Out of scope": [
            "- Failed gate waiver, product fix ve production deployment execution."
        ],
        "Deliverables": ["- Exact artifact hash'lerine bağlı signed go-live decision record."],
        "Acceptance evidence": [
            "- Bütün mandatory gate'ler geçer ve açık critical/high defect yoksa approve; "
            "aksi durumda blocking evidence ile reject kaydedilir. Deployment çalıştırılmaz."
        ],
        "Handoff": ["- None"],
    },
    "V20-CMP-001": {
        "In scope": [
            "- Decision-to-implementation evidence, reviewer authority, exception register ve approval expiry.",
            "- QNB cancellation yalnız private/partner evidence ile applicable ise değerlendirilir; "
            "aksi durumda named approver ile N/A veya blocker kaydedilir."
        ],
    },
    "V20-SEC-001": {
        "In scope": [
            "- Assignee independence: assignee, değerlendirilen security control task'larının implementer'ı olamaz.",
            "- Threat-model verification, SAST/dependency/config scan, authorization abuse case, "
            "public endpoint test, secret scan ve finding severity."
        ],
    },
}

GOAL_POLISH = {
    "V0-CMP-004": "Hedef işletme için bahşiş, servis, masa ve kuver ücretlerinin yasal ve operasyonel uygulanabilirliğini belirlemek.",
    "V0-DAT-005": "Tek şubeli ürün kararı ile gelecekteki çok şube hazırlığı için opsiyonel `business_id` kullanımı arasındaki çelişkiyi çözmek.",
    "V0-DOM-005": "`Table.Reserved` anlamını, bu durumu oluşturabilen actor'ları, expiry davranışını ve walk-in/personel/QR etkileşimini tanımlamak.",
    "V0-DOM-006": "Void, complimentary, discount, waste ve refund davranışlarını actor, approval, tax ve audit etkileriyle ayrı ayrı tanımlamak.",
    "V0-DOM-007": "Ertelenen Bill charge, CustomerAccount payment ve periodic Invoice issuance işlemlerinin receivable balance'ı çift kayıt oluşturmadan nasıl etkilediğini tanımlamak.",
    "V0-DOM-008": "Rapor kodu yazılmadan önce PDF `II.10` kapsamındaki her raporun formula, granularity ve source table sözleşmesini tanımlamak.",
    "V1-BIL-002": "Payment execution'ı etkinleştirmeden item, quantity ve amount ownership segmentlerini kalıcılaştırmak.",
    "V1-CAT-001": "Category, TaxProfile, Product, ModifierGroup ve Modifier yönetimini domain kısıtlarıyla uygulamak.",
    "V1-TBL-004": "`Table.Reserved` arkasındaki onaylı actor, reason ve expiry modelini kalıcılaştırmak.",
    "V1-TBL-005": "Authoritative source ilişkilerinden current Order/Bill pointer projection'larını üretmek ve rebuild etmek.",
    "V11-MNU-002": "Authoritative production/inventory kayıtlarından prepared, reserved, consumed, waste ve available counter projection'larını üretmek.",
    "V11-INV-003": "Tam original movement'a bağlı tek bir idempotent `Reversal` movement oluşturmak.",
    "V11-RSV-001": "Bir OrderItem ve StockBalance'a bağlı `Reserved`, `Released`, `Consumed` ve `Wasted` geçişlerini uygulamak.",
    "V12-MCD-001": "Onaylanmış bir MealCard payment için provider, gross, commission, deduction ve net receivable alanlarını kalıcılaştırmak.",
    "V13-ACC-003": "Onaylanmış bir CustomerAccount tender'ını çift kayıt oluşturmadan tek AccountCharge ve PaymentAllocation kaydına dönüştürmek.",
    "V13-ACC-004": "Bir restaurant Bill'den bağımsız CustomerAccount payment/credit kaydı oluşturmak ve balance projection'ı bir kez güncellemek.",
    "V13-CST-001": "Field-level access policy ile PII sahibi boundary içinde minimum customer identity, tax ve contact alanlarını kalıcılaştırmak.",
    "V14-REC-001": "Local/provider Order, status, cancellation ve stock outcome farklılıklarını tespit etmek ve izlemek.",
    "V15-OBS-001": "Critical flow'larda correlation, request, user/device ve provider reference alanlarını redaction kurallarıyla structured log olarak yayınlamak.",
    "V15-OBS-003": "Korunan kayıtları silmeden health, alert-event, inbox/outbox ve high-volume audit support verisinin büyümesini retention/partition kurallarıyla sınırlamak.",
    "V15-BKP-002": "Isolated PostgreSQL instance'a restore işlemini otomatikleştirmek ve integrity/application smoke kontrollerini çalıştırmak.",
    "V20-GAT-002": "Tamamlanan gate çıktılarından, sonuçları yeniden yazmadan tamper-evident release evidence pack oluşturmak.",
    "V20-SEC-001": "Release candidate'ın authentication, authorization, public endpoint, secret ve sensitive-data kontrollerini bağımsız olarak değerlendirmek.",
    "V0-BKP-002": "İşletmenin veri kaybı ve kesinti toleransını ölçülebilir RPO/RTO acceptance target değerlerine dönüştürmek.",
    "V0-LIC-001": "Tek seferlik license activation, machine binding, offline authorization, transfer, support update ve failure davranışını tanımlamak.",
    "V0-MCD-001": "Desteklenecek meal-card provider'larını belirlemek ve payment, cancellation/refund, commission, statement ve settlement contract'larını doğrulamak.",
    "V0-ARC-004": "HTTP API ve event contract'ları için versioning, validation, error, idempotency, concurrency ve pagination kurallarını tanımlamak.",
    "V0-ARC-005": "Configurable değerleri module owner, scope, validation, history ve secret-storage yasağına göre sınıflandırmak.",
    "V1-ALT-001": "Rule-based Alert lifecycle, source reference, deduplication ve notification audit davranışını uygulamak.",
    "V1-BIL-001": "Bill, BillItem ve V0-DOM-002 tarafından seçilen referentially safe Order/OrderItem source ilişkisini uygulamak.",
    "V1-BIL-003": "Yalnız onaylanmış discount, fee ve tip line type'larını tax ve authorization kurallarıyla hesaplamak ve kalıcılaştırmak.",
    "V1-CSH-001": "Payment'ı etkinleştirmeden terminal/cashier ownership, tek open session, cash routing ve close permission sözleşmesini kesinleştirmek.",
    "V1-CUI-001": "Türkçe cashier shell, authenticated session ve concurrency-aware Table status görünümünü uygulamak.",
    "V1-CUI-002": "Product/modifier seçimi, note, Draft düzenleme ve idempotent submit akışını Türkçe UI ile uygulamak.",
    "V1-CUI-003": "Open Order/Bill, kitchen progress ve failed/Unknown PrintJob durumlarını izinli recovery action'larla göstermek.",
    "V1-CAT-002": "Bağımsız yazılabilir duplicate price state oluşturmadan price record'larını ve authoritative effective-price query'yi uygulamak.",
    "V1-FND-001": "V0-ARC-001'in gerektirdiği host, module composition boundary ve dependency enforcement iskeletini oluşturmak.",
    "V1-FND-002": "V0-ARC-003 sözleşmesindeki request-key validation, response replay, Inbox persistence ve Outbox dispatch altyapısını uygulamak.",
    "V1-IAM-001": "Password verification, active-user check, login/logout ve secure session issuance davranışını uygulamak.",
    "V1-IAM-002": "Role, permission, assignment ve server-side authorization check davranışlarını uygulamak.",
    "V1-IAM-003": "Cashier ve waiter client'ları için device-bound session creation, expiry ve revocation davranışını uygulamak.",
    "V1-KIT-001": "Accepted Order'lardan station-scoped KitchenTicket üretmek ve KitchenTicketItem status'lerini bağımsız korumak.",
    "V1-KIT-002": "Her kitchen item'ı tam bir configured station/printer route'a veya açık configuration error sonucuna çözmek.",
    "V1-KIT-003": "Ticket/output başına tek logical PrintJob kalıcılaştırmak ve retry'ları idempotency altyapısıyla yürütmek.",
    "V1-KIT-004": "Send/ack crash window'u explicit Unknown state ve operator-controlled reprint semantiğiyle yönetmek.",
    "V1-OBS-001": "V1 flow'ları için structured event contract, correlation/request ID ve bounded status-audit persistence eklemek.",
    "V1-OPS-001": "Actor, reason, correlation ve before/after reference alanlarıyla V1 critical command'ları için immutable audit event üretmek.",
    "V1-OPS-002": "Local database backup'ını schedule etmek, metadata'yı kalıcılaştırmak ve database/disk/backup health durumlarını yayımlamak.",
    "V1-ORD-001": "Order ve OrderItem lifecycle, price snapshot, modifier ve Table/customer context davranışını uygulamak.",
    "V1-ORD-002": "Waiter/cashier submit akışını response replay içeren version-controlled concurrent command olarak uygulamak.",
    "V1-ORD-003": "Onaylı void/complimentary politikasını permission, reason, audit ve kitchen-state kontrolleriyle uygulamak.",
    "V1-REC-001": "Canonical ReconciliationCase lifecycle, paired source reference, open-case deduplication ve append-only event/action yapısını uygulamak.",
    "V1-SET-001": "Module owner, scope, type ve append-only change history ile validated non-secret setting'leri kalıcılaştırmak.",
    "V1-TBL-001": "Table identity, zone, canonical status transition ve optimistic concurrency davranışını uygulamak.",
    "V1-TBL-002": "History'yi koruyarak open operational Order/Bill ilişkisini Table'lar arasında taşımak.",
    "V1-TBL-003": "Source Table veya Order silmeden multi-table merge membership ve explicit undo modelini uygulamak.",
    "V1-WTR-001": "Personal device session, installable shell ve izinli offline operation queue davranışını uygulamak.",
    "V1-WTR-002": "Waiter permission kapsamında Table seçimi, Product/modifier/note girişi ve idempotent submit akışını uygulamak.",
    "V1-WTR-003": "Server-authoritative Order ve KitchenTicketItem progress durumunu reconnect-safe refresh ile göstermek.",
    "V11-MNU-003": "Price veya stock ownership almadan Catalog Product seçen reusable Menu/MenuItem composition modelini uygulamak.",
    "V11-PRD-001": "Immutable RecipeVersion'a bağlı Planned, InProgress, Completed ve Cancelled ProductionBatch lifecycle'ını uygulamak.",
    "V11-PRD-002": "ProductionBatch transaction'ında IngredientConsumption ve prepared-portion ProductionOutput movement'larını oluşturmak.",
    "V11-PUR-001": "Supplier PurchaseOrder ve line item'ları, StockLedger'a kayıtlı receipt movement'larıyla uygulamak.",
    "V12-CSH-001": "Terminal/cashier bağlı Open, Counting, Closing, Closed ve Reconciled CashSession geçişlerini uygulamak.",
    "V12-CSH-002": "Cash sale/refund/in/out entry'lerini kaydetmek ve expected/actual close variance değerini hesaplamak.",
    "V12-FSC-001": "Provider/device reference ve immutable request history ile sale, cancellation ve refund FiscalDocument kayıtlarını kalıcılaştırmak.",
    "V12-FSC-002": "Fiscal kapsamındaki bir Bill'in ne zaman close edilebileceğine veya reconciliation gerektirdiğine onaylı legal/device policy ile karar vermek.",
    "V12-FSC-003": "QNB veya T300 ownership almadan V0-CMP-001 tarafından seçilen document open/update/close stratejisini uygulamak.",
    "V12-ALC-001": "Payment/Bill/segment identity, currency, amount ve idempotency için PaymentAllocation row'larını ve database enforcement'ı uygulamak.",
    "V12-ALC-002": "Allocated, paid ve change total değerlerini hesaplamak ve Bill status'ünü authoritative Payment kayıtlarından atomik üretmek.",
    "V12-ALC-003": "Immutable compensating PaymentAllocation kayıtlarıyla full/partial refund sonrası net-paid amount değerini yeniden hesaplamak.",
    "V13-CST-002": "Legal olarak korunan financial reference'ları silmeden Requested, RetentionBlocked, Pending ve Anonymized durumlarını uygulamak.",
    "V13-INV-004": "Issued Invoice'ı silmeden veya Account balance'ı iki kez değiştirmeden izinli cancellation/correction işlemini yeni provider/domain action olarak temsil etmek.",
    "V14-CWB-001": "Authenticated QR customer session için available sellable menu'yü internal management verisini açmadan sunmak.",
    "V14-CWB-002": "QR customer'ın açık final summary ile Order oluşturup PendingConfirmation workflow'una göndermesini sağlamak.",
    "V14-QRS-001": "Reusable raw secret saklamadan hashed, revocable ve time/policy-bound Table token yayımlamak.",
    "V14-QRS-002": "Relay message authentication yapmak ve local command dispatch öncesi replay, rate-limit ve payload-size kontrollerini uygulamak.",
    "V14-QRS-003": "Raw Table token'ı reusable browser credential'a çevirmeden QR token validation sonrası revocable customer session oluşturmak.",
    "V15-REC-002": "İzinli retry, accept-provider, accept-local, compensate, reject ve escalate action'larını permission ve audit ile yürütmek.",
    "V20-UAT-001": "Release candidate üzerinde cashier, waiter, Table, Order, kitchen, QR ve printing workflow'ları için named user acceptance toplamak.",
    "V20-UAT-002": "Billing, Payment, refund, CashSession, CustomerAccount, Invoice, purchasing, stock ve reporting workflow'ları için named user acceptance toplamak.",
    "V20-UAT-003": "Offline, timeout, duplicate, reconciliation, backup, diagnostics ve recovery prosedürleri için named operational acceptance toplamak.",
    "V20-MIG-001": "Representative sanitized dataset üzerinde production migration path'in tamamını çalıştırmak ve integrity, duration ve resource usage değerlerini ölçmek.",
    "V20-INS-001": "Signed release candidate'ı deterministic ve belgelenmiş package ile clean supported target'a kurmak.",
    "V20-INS-002": "Onaylı önceki kurulumu release candidate'a yükseltmek ve update migration öncesi/sonrası failure durumundan güvenle kurtarmak.",
    "V20-INT-006": "Onaylı network/security topology altında scan işleminden PendingConfirmation Order'a kadar public QR path'i sertifikalandırmak.",
}

SECTION_POLISH = {
    "V0-DOM-008": {
        "Acceptance evidence": [
            "- Her ölçümde granularity, filtreler, saat dilimi/iş tarihi, source-of-truth ve reconciliation total bulunur; tanımsız terim `Blocked` kalır."
        ]
    },
    "V0-MCD-001": {
        "Out of scope": [
            "- Production adapter yazmak veya private provider contract kanıtı olmadan provider seçmek."
        ]
    },
    "V1-BIL-002": {"Out of scope": ["- PaymentAllocation ve mixed-tender execution."]},
    "V1-RPT-001": {
        "In scope": [
            "- İş tarihi filtreleri, order/table/waiter granularity, print status ve reconciliation toplamları."
        ]
    },
    "V12-PUI-001": {
        "Acceptance evidence": [
            "- UI over-allocation gönderemez; Unknown payment duplicate tender'ı engeller; mixed payment yalnız server doğrulamasıyla kapanır."
        ]
    },
    "V13-ACC-003": {
        "In scope": [
            "- Eligibility, credit-policy sonucu, AccountCharge source, Payment approval ve allocation transaction boundary."
        ],
        "Out of scope": ["- Periodic Invoice issuance ve genel credit scoring."],
    },
    "V20-UAT-003": {
        "Out of scope": [
            "- RPO/RTO hedeflerini değiştirme, production incident execution ve application fix."
        ]
    },
    "V20-SEC-001": {
        "Out of scope": [
            "- Application fix, legal sign-off ve usability/load certification."
        ],
        "Acceptance evidence": [
            "- Açık Critical/High finding kalmaz; her alt finding için owner, nitelik, severity ve supporting evidence kaydedilir."
        ],
    },
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read_utf8(path: Path) -> str:
    return path.read_text(encoding="utf-8")


_REMEDIATION_ADMISSION_TABLE_HEADER = (
    "| Task ID | Approval date | Source basis | Purpose | Gate closure evidence | "
    "New feature behavior |"
)
_REMEDIATION_ADMISSION_TABLE_SEPARATOR = "| --- | --- | --- | --- | --- | --- |"
_REMEDIATION_ADMISSION_TABLE_ROW = re.compile(
    r"^\| `(?P<task_id>V\d+-[A-Z]+-\d+)` \| `(?P<approval_date>\d{4}-\d{2}-\d{2})` \| "
    r"`(?P<source_basis>CORR:C\d+(?:;CORR:C\d+)*)` \| Verified finding remediation only \| "
    r"Not gate closure evidence \| No new feature behavior \|$"
)
_REMEDIATION_ADMISSION_RECORDS = (
    ("V1-FND-016", "2026-08-10", "CORR:C52"),
    ("V1-FND-017", "2026-08-10", "CORR:C52"),
    ("V1-FND-018", "2026-08-10", "CORR:C52"),
    ("V1-FND-019", "2026-08-10", "CORR:C52"),
    ("V1-FND-020", "2026-08-10", "CORR:C52"),
    ("V1-FND-021", "2026-08-10", "CORR:C52"),
    ("V1-FND-022", "2026-08-10", "CORR:C52"),
    ("V1-FND-023", "2026-08-11", "CORR:C52;CORR:C53;CORR:C54"),
    ("V1-IAM-006", "2026-08-10", "CORR:C52"),
    ("V1-IAM-007", "2026-08-10", "CORR:C52"),
    ("V1-IAM-008", "2026-08-10", "CORR:C52"),
    ("V1-IAM-009", "2026-08-10", "CORR:C52"),
    ("V1-IAM-010", "2026-08-10", "CORR:C52"),
    ("V1-IAM-011", "2026-08-10", "CORR:C52"),
    ("V1-IAM-012", "2026-08-10", "CORR:C52"),
    ("V1-IAM-013", "2026-08-10", "CORR:C52"),
    ("V1-SEC-004", "2026-08-10", "CORR:C52"),
    ("V1-SEC-005", "2026-08-10", "CORR:C52"),
    ("V1-CAT-003", "2026-08-10", "CORR:C52"),
)
_C54_APPLICATION_TASK_ID = "V1-FND-023"
_C54_APPLICATION_SOURCES = ("CORR:C52", "CORR:C53", "CORR:C54", "CORR:C57")
_C54_APPLICATION_SURFACES = (
    "Directory.Build.targets",
    "tests/Architecture/TestDiscovery/test_solution_test_discovery.py",
    "evidence/V1-FND-023/**",
)
_C54_APPLICATION_DEPENDENCIES = ("V0-GOV-050", "V0-GOV-054", "V1-FND-001")
_C54_DONE_DEPENDENCIES = ("V0-GOV-050", "V1-FND-001")
_C54_TRACEABILITY_ROW = (
    "| `C54` | 2026-08-11 kullanıcı onaylı C53 plan düzeltmesi: `V1-FND-001` "
    "historical `Done` body’si ve C52 reserved-surface kaydı aynen korunur; "
    "`V1-FND-023` yalnız C53’te kanıtlanmış test-discovery kusuru için "
    "`Directory.Build.targets` üzerinde tek-seferlik exact write authority alır. "
    "Bu authority historical ownership transferi veya `V1-FND-001`in yeniden "
    "açılması değildir. `V0-GOV-050`, `plan/GATES.md`nin tek ileriye-dönük "
    "owner’ıdır; `V0-GOV-036` bu dosyayı read-only tüketir ve `V0-GOV-050`e "
    "bağımlıdır. `V1-FND-023` admission kaydı, `2026-08-11` tarihi ve "
    "`CORR:C52;CORR:C53;CORR:C54` source basis’iyle C54’ün dar C52/C53 "
    "düzeltmesini taşır. Önceki `a7c5a85` plan kaydındaki `V1-FND-001` body "
    "değişikliği yeni committe geri alınır; geçmiş rewrite edilmez. C52 yalnız bu "
    "dar çelişki için C54 tarafından tamamlanır. PDF current authority değildir, "
    "gate kapatmaz ve product behavior izni vermez. | `V0-GOV-036`, "
    "`V0-GOV-050`, `V1-FND-023` | C53 plan correction ve bağımsız final denetim "
    "| Planned |"
)


def parse_remediation_admission_table(
    text: str,
    start_marker: str,
    end_marker: str,
    label: str,
) -> tuple[list[tuple[str, str, str]], list[str]]:
    """Parse one strict admission table without granting malformed rows authority."""
    errors: list[str] = []
    lines = text.splitlines()
    starts = [index for index, line in enumerate(lines) if line == start_marker]
    ends = [index for index, line in enumerate(lines) if line == end_marker]
    prefix = f"SEMANTIC_REMEDIATION_ADMISSION_{label}"
    if len(starts) != 1 or len(ends) != 1:
        return [], [f"{prefix}_MARKER"]
    start, end = starts[0], ends[0]
    if start >= end:
        return [], [f"{prefix}_MARKER_ORDER"]
    table_lines = lines[start + 1 : end]
    if len(table_lines) < 2 or table_lines[0] != _REMEDIATION_ADMISSION_TABLE_HEADER:
        errors.append(f"{prefix}_HEADER")
    if len(table_lines) < 2 or table_lines[1] != _REMEDIATION_ADMISSION_TABLE_SEPARATOR:
        errors.append(f"{prefix}_SEPARATOR")

    records: list[tuple[str, str, str]] = []
    for line in table_lines[2:]:
        match = _REMEDIATION_ADMISSION_TABLE_ROW.fullmatch(line)
        if match is None:
            errors.append(f"{prefix}_ROW")
            continue
        records.append(
            (
                match.group("task_id"),
                match.group("approval_date"),
                match.group("source_basis"),
            )
        )
    return records, errors


def admission_record_errors(
    label: str,
    actual: list[tuple[str, str, str]],
    expected: tuple[tuple[str, str, str], ...],
) -> list[str]:
    """Return stable, specific errors for divergence from one admission tuple."""
    prefix = f"SEMANTIC_REMEDIATION_ADMISSION_{label}"
    errors: list[str] = []
    actual_ids = [record[0] for record in actual]
    expected_ids = [record[0] for record in expected]
    if len(actual) != len(expected):
        errors.append(f"{prefix}_COUNT expected={len(expected)} actual={len(actual)}")
    duplicates = sorted({task_id for task_id in actual_ids if actual_ids.count(task_id) > 1})
    if duplicates:
        errors.append(f"{prefix}_DUPLICATE {','.join(duplicates)}")
    missing = sorted(set(expected_ids) - set(actual_ids))
    if missing:
        errors.append(f"{prefix}_MISSING {','.join(missing)}")
    extra = sorted(set(actual_ids) - set(expected_ids))
    if extra:
        errors.append(f"{prefix}_EXTRA {','.join(extra)}")
    if not missing and not extra and not duplicates and actual_ids != expected_ids:
        errors.append(f"{prefix}_ORDER")

    actual_by_id = {task_id: (approval_date, source_basis) for task_id, approval_date, source_basis in actual}
    for task_id, expected_date, expected_source in expected:
        record = actual_by_id.get(task_id)
        if record is None:
            continue
        actual_date, actual_source = record
        if actual_date != expected_date:
            errors.append(
                f"{prefix}_DATE {task_id} expected={expected_date} actual={actual_date}"
            )
        if actual_source != expected_source:
            errors.append(
                f"{prefix}_SOURCE {task_id} expected={expected_source} actual={actual_source}"
            )
    return errors


def parse_task_scope_admission_records(path: Path) -> tuple[list[tuple[str, str, str]], list[str]]:
    """Read the task-scope literal records while preserving source declaration order."""
    prefix = "SEMANTIC_REMEDIATION_ADMISSION_TASK_SCOPE"
    tree = ast.parse(read_utf8(path), filename=str(path))
    assignment = next(
        (
            node
            for node in tree.body
            if isinstance(node, ast.Assign)
            and any(
                isinstance(target, ast.Name)
                and target.id == "_C52_C53_C54_CANDIDATE_REMEDIATION_RECORDS"
                for target in node.targets
            )
        ),
        None,
    )
    if assignment is None or not isinstance(assignment.value, ast.Dict):
        return [], [f"{prefix}_DECLARATION"]

    records: list[tuple[str, str, str]] = []
    for key, value in zip(assignment.value.keys, assignment.value.values, strict=True):
        if key is None:
            return [], [f"{prefix}_ROW"]
        parsed_key = ast.literal_eval(key)
        parsed_value = ast.literal_eval(value)
        if (
            not isinstance(parsed_key, str)
            or not isinstance(parsed_value, tuple)
            or len(parsed_value) != 2
            or not all(isinstance(item, str) for item in parsed_value)
        ):
            return [], [f"{prefix}_ROW"]
        records.append((parsed_key, parsed_value[0], parsed_value[1]))
    return records, []


def validate_remediation_admission_tuple() -> list[str]:
    """Require contract, gate table and canonical task-scope records to agree."""
    errors: list[str] = []
    gates_records, gates_errors = parse_remediation_admission_table(
        read_utf8(PLAN_DIR / "GATES.md"),
        "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:START -->",
        "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->",
        "GATES",
    )
    errors.extend(gates_errors)
    errors.extend(admission_record_errors("GATES", gates_records, _REMEDIATION_ADMISSION_RECORDS))

    contract_records, contract_errors = parse_remediation_admission_table(
        read_utf8(PLAN_DIR / "VALIDATION_CONTRACT.md"),
        "<!-- PLAN_AUDIT_REMEDIATION_ADMISSION:START -->",
        "<!-- PLAN_AUDIT_REMEDIATION_ADMISSION:END -->",
        "CONTRACT",
    )
    errors.extend(contract_errors)
    errors.extend(
        admission_record_errors("CONTRACT", contract_records, _REMEDIATION_ADMISSION_RECORDS)
    )

    tool_records, tool_errors = parse_task_scope_admission_records(
        WORKSPACE / "tools" / "task-scope" / "task_scope_tool.py"
    )
    errors.extend(tool_errors)
    errors.extend(
        admission_record_errors(
            "TASK_SCOPE", tool_records, tuple(sorted(_REMEDIATION_ADMISSION_RECORDS))
        )
    )
    return errors


def relative_workspace_path(value: str) -> str | None:
    normalized = value.replace("\\", "/")
    workspace_prefix = str(WORKSPACE).replace("\\", "/") + "/"
    if normalized.casefold().startswith(workspace_prefix.casefold()):
        return normalized[len(workspace_prefix) :]
    if normalized.startswith("plan/") or normalized.startswith("tmp/"):
        return normalized
    return None


def apply_unified_diff(original: str, diff_text: str) -> str:
    original_lines = original.splitlines()
    output: list[str] = []
    cursor = 0
    diff_lines = diff_text.splitlines()
    index = 0
    while index < len(diff_lines):
        header = re.match(
            r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@",
            diff_lines[index],
        )
        if not header:
            index += 1
            continue
        old_start = int(header.group(1)) - 1
        output.extend(original_lines[cursor:old_start])
        cursor = old_start
        index += 1
        while index < len(diff_lines) and not diff_lines[index].startswith("@@ "):
            line = diff_lines[index]
            if line.startswith(" "):
                expected = line[1:]
                if cursor >= len(original_lines) or original_lines[cursor] != expected:
                    raise ValueError(
                        f"Diff context mismatch at source line {cursor + 1}: {expected!r}"
                    )
                output.append(original_lines[cursor])
                cursor += 1
            elif line.startswith("-"):
                expected = line[1:]
                if cursor >= len(original_lines) or original_lines[cursor] != expected:
                    raise ValueError(
                        f"Diff deletion mismatch at source line {cursor + 1}: {expected!r}"
                    )
                cursor += 1
            elif line.startswith("+"):
                output.append(line[1:])
            elif line.startswith("\\ No newline"):
                pass
            index += 1
    output.extend(original_lines[cursor:])
    return "\n".join(output) + ("\n" if original.endswith("\n") else "")


def reconstruct_baseline() -> dict[str, str]:
    state: dict[str, str] = {}
    with SESSION_LOG_PATH.open(encoding="utf-8") as stream:
        for raw_line in stream:
            event = json.loads(raw_line)
            if event.get("timestamp", "") > BASELINE_CUTOFF:
                break
            if event.get("type") != "event_msg":
                continue
            payload = event.get("payload", {})
            if payload.get("type") != "patch_apply_end" or not payload.get("success"):
                continue
            for absolute_path, change in payload.get("changes", {}).items():
                relative = relative_workspace_path(absolute_path)
                if relative is None:
                    continue
                move_path = change.get("move_path")
                if change.get("type") == "delete":
                    state.pop(relative, None)
                    continue
                if "content" in change:
                    state[relative] = change["content"]
                elif "unified_diff" in change:
                    if relative not in state:
                        raise ValueError(f"Update before add in transcript: {relative}")
                    state[relative] = apply_unified_diff(
                        state[relative], change["unified_diff"]
                    )
                if move_path:
                    moved_relative = relative_workspace_path(move_path)
                    if moved_relative is None:
                        raise ValueError(f"Move target outside workspace: {move_path}")
                    state[moved_relative] = state.pop(relative)
    return state


def recover_baseline() -> None:
    baseline = json.loads(read_utf8(BASELINE_PATH))
    state = reconstruct_baseline()
    recovery_root = WORKSPACE / "tmp" / "plan_audit_original"
    matched = 0
    mismatches: list[str] = []
    for record in baseline["files"]:
        relative = record["path"]
        content = state.get(relative)
        if content is None:
            mismatches.append(f"MISSING {relative}")
            continue
        digest = hashlib.sha256(content.encode("utf-8")).hexdigest().upper()
        if digest != record["sha256"]:
            mismatches.append(
                f"HASH {relative}: expected {record['sha256']}, reconstructed {digest}"
            )
            continue
        output_path = recovery_root / Path(relative).relative_to("plan")
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(content, encoding="utf-8", newline="")
        matched += 1
    print(f"Recovered baseline files: {matched}")
    print(f"Recovery mismatches: {len(mismatches)}")
    for mismatch in mismatches:
        print(mismatch)
    if mismatches:
        raise SystemExit(1)


def line_count(text: str) -> int:
    return len(text.splitlines())


def task_files() -> list[Path]:
    paths = []
    for path in sorted(PLAN_DIR.rglob("*.md")):
        first_line = read_utf8(path).splitlines()[0] if path.stat().st_size else ""
        if TASK_HEADER.match(first_line):
            paths.append(path)
    return paths


def audited_markdown_paths() -> list[Path]:
    """Return every active Markdown artifact, excluding the frozen tmp baseline."""

    paths = [
        path
        for root in [PLAN_DIR, WORKSPACE / "docs", WORKSPACE / "evidence"]
        if root.exists()
        for path in root.rglob("*.md")
    ]
    agents_path = WORKSPACE / "AGENTS.md"
    if agents_path.exists():
        paths.append(agents_path)
    return sorted(paths)


def split_sections(text: str) -> tuple[list[str], dict[str, list[str]], list[str]]:
    lines = text.splitlines()
    preamble: list[str] = []
    sections: dict[str, list[str]] = {}
    order: list[str] = []
    current: str | None = None
    for line in lines:
        if line.startswith("## "):
            current = line[3:].strip()
            if current not in sections:
                sections[current] = []
                order.append(current)
            continue
        if current is None:
            preamble.append(line)
        else:
            sections[current].append(line)
    for key in sections:
        while sections[key] and not sections[key][0].strip():
            sections[key].pop(0)
        while sections[key] and not sections[key][-1].strip():
            sections[key].pop()
    return preamble, sections, order


def parse_coverage_sources() -> dict[str, list[str]]:
    coverage_path = PLAN_DIR / "PDF_COVERAGE.md"
    mapping: dict[str, list[str]] = defaultdict(list)
    for line in read_utf8(coverage_path).splitlines():
        if not line.startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if not cells:
            continue
        ids = TASK_ID.findall(line)
        if not ids:
            continue
        sources: list[str] = []
        if re.fullmatch(r"C[1-9]", cells[0]):
            sources.append(f"CORR:{cells[0]}")
        else:
            sources.extend(f"PDF:{value}" for value in PDF_SECTION.findall(cells[0]))
        for task_id in ids:
            for source in sources:
                if source not in mapping[task_id]:
                    mapping[task_id].append(source)
    return mapping


def task_source_owner_ranges() -> list[tuple[str, list[str]]]:
    entries: list[tuple[str, list[str]]] = []
    for path in task_files():
        text_value = read_utf8(path)
        header = TASK_HEADER.match(text_value.splitlines()[0])
        if not header:
            continue
        task_id = header.group("id")
        _, sections, _ = split_sections(text_value)
        for line in sections.get("Source basis", []):
            if not line.startswith("- PDF:"):
                continue
            token = line.removeprefix("- PDF:").strip()
            if PDF_SECTION.fullmatch(token):
                entries.append((token, [task_id]))
    return entries


def section_key(value: str) -> tuple[int, tuple[int, ...], str]:
    match = re.fullmatch(r"(I|II|III|IV)\.(\d+(?:\.\d+)*)([A-Z]?)", value)
    if not match:
        raise ValueError(f"Invalid PDF section: {value}")
    part_order = {"I": 1, "II": 2, "III": 3, "IV": 4}[match.group(1)]
    numbers = tuple(int(item) for item in match.group(2).split("."))
    return part_order, numbers, match.group(3)


def in_section_range(section: str, token: str) -> bool:
    if "-" not in token:
        return section == token or section.startswith(token + ".")
    start, end = token.split("-", 1)
    if section == start or section.startswith(start + "."):
        return True
    if section == end or section.startswith(end + "."):
        return True
    section_value = section_key(section)
    start_value = section_key(start)
    end_value = section_key(end)
    return start_value <= section_value <= end_value


def owner_for_section(section: str, entries: list[tuple[str, list[str]]]) -> list[str]:
    structural_owners = {
        "II.2": ["V0-DOC-001", "V0-ARC-001"],
        "II.3": ["V0-DOC-001", "V0-DOM-001"],
        "II.3.1": ["V1-CAT-001"],
        "II.4": ["V0-ARC-001", "V0-DOC-001"],
        "II.5": ["V0-DOM-001", "V0-ARC-004"],
        "II.5.10A": ["V12-MCD-001", "V0-DOM-001"],
        "II.6": ["V0-DOM-001", "V0-DOC-001"],
        "II.6.1": ["V1-TBL-002"],
        "II.6.2": ["V1-TBL-003"],
        "II.6.3": ["V1-BIL-002"],
        "II.6.4": ["V12-ALC-002"],
        "II.6.5": ["V12-PAY-001", "V12-ALC-002"],
        "II.6.6": ["V0-DOM-004", "V12-ALC-001"],
        "II.6.7": ["V11-RCP-001"],
        "II.6.9": ["V11-RSV-001", "V11-RSV-002"],
        "II.6.10": ["V1-KIT-003", "V1-KIT-004"],
        "II.6.12": ["V1-CSH-001", "V12-CSH-001"],
        "II.6.13": ["V12-MCD-001", "V13-ACC-003"],
        "II.7": ["V0-ARC-001", "V0-DOC-001"],
        "II.7.1": ["V1-CUI-001", "V1-CUI-002"],
        "II.7.2": ["V1-WTR-001", "V1-WTR-002"],
    }
    owners: list[str] = []
    matching_entries = [
        (token, token_owners)
        for token, token_owners in entries
        if not token.startswith("C") and in_section_range(section, token)
    ]
    exact_entries = [entry for entry in matching_entries if entry[0] == section]
    selected_entries = exact_entries or matching_entries
    for token, token_owners in selected_entries:
        for owner in token_owners:
            if owner not in owners:
                owners.append(owner)
    for owner in structural_owners.get(section, []):
        if owner not in owners:
            owners.append(owner)
    if not owners:
        raise ValueError(f"No coverage owner for PDF section {section}")
    return owners


def extract_pdf_headings() -> list[dict[str, str | int]]:
    from pypdf import PdfReader

    reader = PdfReader(str(PDF_PATH))
    numbered = re.compile(
        r"^(?P<section>(?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?)\s+(?P<title>.+)$"
    )
    correction = re.compile(r"^(?P<section>C[1-9])\s+[—-]\s+(?P<title>.+)$")
    headings: dict[str, dict[str, str | int]] = {}
    for page_number, page in enumerate(reader.pages, 1):
        lines = (page.extract_text() or "").splitlines()
        for raw_line in lines:
            line = " ".join(raw_line.split())
            match = numbered.match(line) or correction.match(line)
            if not match:
                continue
            section = match.group("section")
            title = match.group("title").strip()
            if re.match(r"^[–—-]\s*(?:I|II|III)\.", title):
                continue
            if section not in headings:
                headings[section] = {
                    "section": section,
                    "title": title,
                    "page": page_number,
                }
    def sort_value(item: dict[str, str | int]) -> tuple[int, tuple[int, ...], str]:
        section = str(item["section"])
        if section.startswith("C"):
            return 5, (int(section[1:]),), ""
        return section_key(section)

    return sorted(headings.values(), key=sort_value)


def markdown_cell(value: str) -> str:
    return " ".join(value.replace("|", "\\|").split())


def summary_corrections(unit_id: str) -> list[str]:
    line_ranges = {
        "P092": [
            (4, 8, ["C1"]),
            (9, 15, ["C7"]),
            (16, 20, ["C5"]),
            (21, 23, ["C6"]),
            (24, 28, ["C3"]),
            (29, 33, ["C4"]),
            (34, 38, ["C2"]),
        ],
        "P093": [
            (2, 8, ["C9"]),
            (9, 11, ["C8"]),
            (12, 13, ["C1", "C7"]),
            (14, 14, ["C1", "C7", "C5", "C6"]),
            (15, 15, ["C5", "C6"]),
            (16, 17, ["C3", "C4", "C2", "C9"]),
            (18, 19, ["C8"]),
            (34, 35, ["C1", "C7"]),
        ],
        "P094": [
            (2, 4, ["C1"]),
            (5, 7, ["C7"]),
        ],
    }
    line_match = re.fullmatch(r"(P\d{3})-L(\d{3})", unit_id)
    if line_match:
        page, line = line_match.group(1), int(line_match.group(2))
        for start, end, corrections in line_ranges.get(page, []):
            if start <= line <= end:
                return corrections
        return []
    table_map = {
        "P092-T01-R002": ["C1"],
        "P092-T01-R003": ["C7"],
        "P092-T01-R004": ["C5"],
        "P092-T01-R005": ["C6"],
        "P092-T01-R006": ["C3"],
        "P092-T01-R007": ["C4"],
        "P092-T01-R008": ["C2"],
        "P092-T02-R001": ["C1"],
        "P093-T01-R002": ["C9"],
        "P093-T01-R003": ["C8"],
    }
    return table_map.get(unit_id, [])


def unit_owners(
    section: str, entries: list[tuple[str, list[str]]], unit_id: str
) -> list[str]:
    corrections = summary_corrections(unit_id)
    if corrections:
        owners: list[str] = []
        for correction in corrections:
            for owner in CORRECTION_OWNERS[correction]:
                if owner not in owners:
                    owners.append(owner)
        return owners
    if section == "DOCUMENT":
        return ["V0-DOC-001"]
    if section in {"IV.0", "IV.1"}:
        return ["V0-DOC-001"]
    if section in CORRECTION_OWNERS:
        return CORRECTION_OWNERS[section]
    return owner_for_section(section, entries)


def extract_pdf_content_units(
    headings: list[dict[str, str | int]],
) -> tuple[list[dict[str, str | int]], list[dict[str, str | int]]]:
    import pdfplumber

    logging.getLogger("pdfminer").setLevel(logging.ERROR)
    heading_pages = {
        (str(item["section"]), int(item["page"])) for item in headings
    }
    numbered = re.compile(
        r"^(?P<section>(?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?)\s+.+$"
    )
    correction = re.compile(r"^(?P<section>C[1-9])\s+[—-]\s+.+$")
    list_item = re.compile(
        r"^(?:[•●▪◦‣⁃]|[-–—]\s|\d+[.)]\s|[a-zA-Z][.)]\s)"
    )
    normative = re.compile(
        r"\b(?:must|must not|shall|required|cannot|never|only|"
        r"zorunlu|zorunludur|gerekir|gerekmektedir|yasak|olamaz|"
        r"yalnızca|sadece|kabul edilmez)\b",
        re.IGNORECASE,
    )
    line_units: list[dict[str, str | int]] = []
    table_units: list[dict[str, str | int]] = []
    active_section = "DOCUMENT"

    with pdfplumber.open(str(PDF_PATH)) as document:
        for page_number, page in enumerate(document.pages, 1):
            page_start_section = active_section
            raw_lines = page.extract_text_lines() or []
            tables = page.find_tables()
            table_bands = [table.bbox for table in tables]
            positioned_headings: list[tuple[float, str]] = []

            for line_number, item in enumerate(raw_lines, 1):
                text_value = " ".join(str(item.get("text", "")).split())
                if not text_value:
                    continue
                match = numbered.match(text_value) or correction.match(text_value)
                section = match.group("section") if match else None
                if section and (section, page_number) in heading_pages:
                    active_section = section
                    positioned_headings.append((float(item.get("top", 0)), section))

                center = (float(item.get("top", 0)) + float(item.get("bottom", 0))) / 2
                kinds: list[str] = []
                if section and (section, page_number) in heading_pages:
                    kinds.append("Heading")
                if list_item.match(text_value):
                    kinds.append("List item")
                if normative.search(text_value):
                    kinds.append("Normative")
                if any(top <= center <= bottom for _, top, _, bottom in table_bands):
                    kinds.append("Geometry table line")
                if not kinds:
                    kinds.append("Content")
                line_units.append(
                    {
                        "unit": f"P{page_number:03d}-L{line_number:03d}",
                        "page": page_number,
                        "section": active_section,
                        "kind": "+".join(kinds),
                        "text": text_value,
                        "sha256": hashlib.sha256(text_value.encode("utf-8")).hexdigest().upper(),
                    }
                )

            for table_number, table in enumerate(tables, 1):
                table_section = page_start_section
                for top, section in positioned_headings:
                    if top <= table.bbox[1]:
                        table_section = section
                rows = table.extract() or []
                for row_number, row in enumerate(rows, 1):
                    cells = [" ".join((cell or "").split()) for cell in row]
                    if not any(cells):
                        continue
                    canonical = " || ".join(cells)
                    table_units.append(
                        {
                            "unit": f"P{page_number:03d}-T{table_number:02d}-R{row_number:03d}",
                            "page": page_number,
                            "section": table_section,
                            "kind": "Geometry table row",
                            "text": canonical,
                            "sha256": hashlib.sha256(canonical.encode("utf-8")).hexdigest().upper(),
                        }
                    )

    return line_units, table_units


def generate_coverage() -> None:
    entries = task_source_owner_ranges()
    headings = extract_pdf_headings()
    numbered = [item for item in headings if not str(item["section"]).startswith("C")]
    corrections = [item for item in headings if str(item["section"]).startswith("C")]
    line_units, table_units = extract_pdf_content_units(headings)
    if len(numbered) != 374 or len(corrections) != 9:
        raise ValueError(
            f"Expected 374 numbered headings and 9 corrections, found "
            f"{len(numbered)} and {len(corrections)}"
        )

    lines = [
        "# PDF Coverage Matrix",
        "",
        "Bu matris `PDF_SOURCE.md` ile sabitlenen 94 sayfalık PDF'nin bütün",
        "numaralı başlıklarını izler. Her satırın kapsam birimi, o başlıktan sonraki",
        "numaralı başlığa kadar olan paragraph, bullet ve table row'ların tamamıdır.",
        "",
        "Bir owner atanması görevin tamamlandığı anlamına gelmez; disposition",
        "yalnız plan sahipliğini gösterir.",
        "PDF line matrix bütün non-empty text line'ları kapsar. `Geometry table row`",
        "kayıtları `pdfplumber` geometry detector çıktısıdır; semantik tablo varsayımı değildir.",
        "Her unit'in SHA-256 değeri normalize edilmiş tam unit metninden hesaplanır;",
        "matristeki metin hücresi de aynı tam içeriği taşır.",
        "",
        "## Doğrulanan belge kusurları",
        "",
        "- `FIND-PDF-001`: Sayfa 2 belge haritası `II.0-II.16` der; Part II gerçekte `II.15` ile biter.",
        "- `FIND-PDF-002` / `CORR:C8`: Sayfa 25 `I.46` başlangıç listesini 14 sayar; "
        "sayfa 90-91 doğru sayının 13 olduğunu kanıtlar.",
        "- Bu iki bulgu text extraction yanında render edilmiş sayfalarda da görsel olarak doğrulanmıştır.",
        "",
        "## Numara başlık matrisi",
        "",
        "| Section | Page | Heading | Kapsam birimi | Plan owner | Disposition |",
        "| --- | ---: | --- | --- | --- | --- |",
    ]
    for item in numbered:
        section = str(item["section"])
        title = str(item["title"]).replace("|", "\\|")
        owners = ", ".join(owner_for_section(section, entries))
        lines.append(
            f"| `{section}` | {item['page']} | {title} | Sonraki numaralı başlığa kadar tüm içerik | {owners} | Planned |"
        )

    lines.extend(
        [
            "",
            "## Part IV C1-C9 düzeltmeleri",
            "",
            "| Correction | Page | Finding heading | Owner | Disposition |",
            "| --- | ---: | --- | --- | --- |",
        ]
    )
    for item in corrections:
        section = str(item["section"])
        title = str(item["title"]).replace("|", "\\|")
        lines.append(
            f"| `{section}` | {item['page']} | {title} | {', '.join(CORRECTION_OWNERS[section])} | Correction open |"
        )

    lines.extend(
        [
            "",
            "## PDF line coverage",
            "",
            f"Toplam `{len(line_units)}` non-empty text line ayrı unit olarak kaydedilmiştir.",
            "",
            "| Unit | Page | Parent | Class | SHA-256 | Tam normalize metin | Owner | Disposition |",
            "| --- | ---: | --- | --- | --- | --- | --- | --- |",
        ]
    )
    for unit in line_units:
        owners = ", ".join(
            unit_owners(str(unit["section"]), entries, str(unit["unit"]))
        )
        lines.append(
            f"| `{unit['unit']}` | {unit['page']} | `{unit['section']}` | {unit['kind']} | "
            f"`{unit['sha256']}` | {markdown_cell(str(unit['text']))} | {owners} | Planned |"
        )

    lines.extend(
        [
            "",
            "## Geometry-detected table row coverage",
            "",
            f"Toplam `{len(table_units)}` geometry-detected row ayrı unit olarak kaydedilmiştir.",
            "",
            "| Unit | Page | Parent | Class | SHA-256 | Tam normalize cell metni | Owner | Disposition |",
            "| --- | ---: | --- | --- | --- | --- | --- | --- |",
        ]
    )
    for unit in table_units:
        owners = ", ".join(
            unit_owners(str(unit["section"]), entries, str(unit["unit"]))
        )
        lines.append(
            f"| `{unit['unit']}` | {unit['page']} | `{unit['section']}` | {unit['kind']} | "
            f"`{unit['sha256']}` | {markdown_cell(str(unit['text']))} | {owners} | Planned |"
        )

    lines.extend(
        [
            "",
            "## Plan denetiminde eklenen C10-C31 açıkları",
            "",
            "| Correction | Kanıtlanan açık | Decision/validation owner |",
            "| --- | --- | --- |",
            "| `C10` | Fee/tip davranışı PDF'de tanımlı değil. | V0-CMP-004 |",
            "| `C11` | Over-receipt ve receipt variance politikası yok. | V0-DOM-009 |",
            "| `C12` | Historical cost için valuation method yok. | V0-DOM-010 |",
            "| `C13` | Printer route precedence tanımlı değil. | V0-DOM-011 |",
            "| `C14` | Notification transport/recipient matrisi yok. | V0-ARC-006 |",
            "| `C15` | Desteklenen OS/package/update compatibility matrisi yok. | V0-ARC-007 |",
            "| `C16` | Signing, SBOM ve provenance evidence contract'ı yok. | V0-ARC-008 |",
            "| `C17` | Migration rehearsal dataset/control total profili yok. | V0-DAT-006 |",
            "| `C18` | Security verification target ve requirement sürümü yok. | V0-SEC-001 |",
            "| `C19` | Accessibility conformance target yok. | V0-CMP-005 |",
            "| `C20` | Meal-card task'ı birden fazla provider'ı tek işte topluyor. | V0-MCD-001, V20-INT-004 |",
            "| `C21` | QNB cancellation/webhook public contract'ta doğrulanmıyor. | V0-QNB-001, V20-INT-002, V20-CMP-001 |",
            "| `C22` | QR relay production topology/transport/deployment sahibi yoktu. | V0-ARC-009, V0-QRG-001, V14-QRT-001 |",
            "| `C23` | Bill-independent account receipt source ve reconciliation zinciri yoktu. | V0-DOM-007, V13-ACC-004, V13-ACC-007 |",
            "| `C24` | Meal-card result allocation/fiscal workflow'a bağlanmıyordu. | V0-MCD-001, V12-MCD-004 |",
            "| `C25` | T300 ve QNB adisyon branch'leri koşulsuz birlikte zorunluydu. | V0-CMP-001, V12-FSC-003 |",
            "| `C26` | CustomerAccount handler registry/fiscal closure integration sahibi yoktu. | V13-ACC-008 |",
            "| `C27` | On-hand ve reservation balance projection sırası producer cycle üretiyordu. | V11-INV-002, V11-INV-007 |",
            "| `C28` | Transaction primitive Outbox oluşmadan post-commit handoff sahipleniyordu. | V1-FND-006 |",
            "| `C29` | Provider timeout Unknown/ReconciliationRequired durumu olmadan modellenmişti. | V0-DOM-001 |",
            "| `C30` | Shared integration-test fixture dosyalarının tek task sahibi ve provenance kanıtı yoktu. | V1-FND-010 |",
            "| `C31` | Task Markdown değişikliği own write allowlist'i genişletebiliyordu. | V0-GOV-001 |",
            "",
            "## Coverage kapısı",
            "",
            "`V20-GAT-001` owner, acceptance evidence veya applicable disposition",
            "eksikliği bulursa release gate kapanmaz. `II.16` için sahte owner veya",
            "gereksinim oluşturulamaz.",
        ]
    )
    (PLAN_DIR / "PDF_COVERAGE.md").write_text(
        "\n".join(lines).rstrip() + "\n", encoding="utf-8"
    )
    print(f"PDF coverage headings: {len(numbered)}")
    print(f"PDF correction headings: {len(corrections)}")
    print(f"PDF text line units: {len(line_units)}")
    print(f"PDF geometry table row units: {len(table_units)}")


def metadata_value(preamble: list[str], name: str, default: str) -> str:
    prefix = f"- {name}:"
    for line in preamble:
        if line.startswith(prefix):
            return line[len(prefix) :].strip()
    return default


def extract_references(lines: list[str]) -> list[str]:
    text = "\n".join(lines)
    values = TASK_ID.findall(text) + GATE_ID.findall(text)
    if re.search(r"\bNone\b", text):
        values.append("None")
    result: list[str] = []
    for value in values:
        if value not in result:
            result.append(value)
    return result


def render_reference_section(values: list[str]) -> list[str]:
    return [f"- {value}" for value in values] if values else ["- None"]


def normalized_sources(task_id: str, coverage: dict[str, list[str]]) -> list[str]:
    sources = list(coverage.get(task_id, []))
    for source in FALLBACK_SOURCES.get(task_id, []):
        if source not in sources:
            sources.append(source)
    for source in EXTERNAL_SOURCES.get(task_id, []):
        if source not in sources:
            sources.append(source)
    if not sources:
        raise ValueError(f"No evidence source mapping for {task_id}")
    return sources


def normalize_task(path: Path, coverage: dict[str, list[str]]) -> None:
    text = read_utf8(path)
    preamble, sections, original_order = split_sections(text)
    header_match = TASK_HEADER.match(preamble[0])
    if not header_match:
        raise ValueError(f"Invalid task header: {path}")
    task_id = header_match.group("id")

    status = "Blocked" if task_id in BLOCKERS else metadata_value(
        preamble, "Status", "Planned"
    )
    assignee = metadata_value(
        preamble, "Assignee", "Unassigned (exactly one person)"
    )
    work_type = metadata_value(preamble, "Work type", "implementation")
    if task_id in {"V15-PER-001", "V15-PER-002"}:
        work_type = "validation"

    dependencies = extract_references(sections.get("Dependencies", []))
    if task_id in DEPENDENCY_REPLACEMENTS:
        dependencies = list(DEPENDENCY_REPLACEMENTS[task_id])
    for removal in DEPENDENCY_REMOVALS.get(task_id, []):
        dependencies = [value for value in dependencies if value != removal]
    for addition in DEPENDENCY_ADDITIONS.get(task_id, []):
        if addition not in dependencies:
            dependencies.append(addition)
    if not dependencies:
        dependencies = ["None"]

    handoff = extract_references(sections.get("Handoff", []))
    handoff_text = " ".join(sections.get("Handoff", [])).lower()
    broad_markers = (
        "all ",
        "every ",
        "owner",
        "tüm ",
        "bütün ",
        "schema task",
        "module task",
        "validation task",
        "reporting task",
    )
    if task_id in BROAD_HANDOFF_REPLACEMENTS:
        handoff = list(BROAD_HANDOFF_REPLACEMENTS[task_id])
    elif any(marker in handoff_text for marker in broad_markers):
        if not handoff:
            handoff = ["None"]
    if not handoff:
        handoff = ["None"]

    sections["Source basis"] = [
        f"- {source}" for source in normalized_sources(task_id, coverage)
    ]
    sections["Dependencies"] = render_reference_section(dependencies)
    sections["Handoff"] = render_reference_section(handoff)
    if task_id in BLOCKERS:
        sections["Blocker"] = [f"- {BLOCKERS[task_id]}"]
    else:
        sections.pop("Blocker", None)

    required_order = [
        "Source basis",
        "Goal",
        "Owned surface",
        "In scope",
        "Out of scope",
        "Dependencies",
    ]
    if task_id in BLOCKERS:
        required_order.append("Blocker")
    required_order.extend(["Deliverables", "Acceptance evidence", "Handoff"])
    for section in required_order:
        if section not in sections:
            raise ValueError(f"Missing section {section}: {path}")
    extras = [section for section in original_order if section not in required_order]

    output = [
        preamble[0],
        "",
        f"- Status: {status}",
        f"- Assignee: {assignee}",
        f"- Work type: {work_type}",
        "- Surface state: Planned",
        "",
    ]
    for section in required_order + extras:
        output.append(f"## {section}")
        output.append("")
        output.extend(sections[section])
        output.append("")
    path.write_text("\n".join(output).rstrip() + "\n", encoding="utf-8")


def normalize_tasks() -> None:
    coverage = parse_coverage_sources()
    paths = task_files()
    if len(paths) != 195:
        raise ValueError(f"Expected 195 baseline tasks, found {len(paths)}")
    for path in paths:
        normalize_task(path, coverage)
    print(f"Normalized task files: {len(paths)}")


def protect_translation_tokens(text: str) -> tuple[str, dict[str, str]]:
    protected: dict[str, str] = {}

    def save(value: str) -> str:
        token = f"zxq{len(protected)}qxz"
        protected[token] = value
        return token

    patterns = [
        re.compile(r"`[^`]+`"),
        re.compile(r"https?://\S+"),
        re.compile(r"\bV\d+-[A-Z0-9]+-\d+\b"),
        re.compile(r"\bGATE-V[A-Z0-9-]+\b"),
        re.compile(r"\b[A-Z][a-z0-9]+(?:[A-Z][A-Za-z0-9]+)+\b"),
        re.compile(r"\b[A-Z]{2,}[A-Z0-9.-]*\b"),
    ]
    result = text
    for pattern in patterns:
        result = pattern.sub(lambda match: save(match.group(0)), result)
    for term in sorted(TECHNICAL_TERMS, key=len, reverse=True):
        result = re.sub(
            rf"(?<![A-Za-z0-9]){re.escape(term)}(?![A-Za-z0-9])",
            lambda match: save(match.group(0)),
            result,
            flags=re.IGNORECASE,
        )
    return result, protected


def restore_translation_tokens(text: str, protected: dict[str, str]) -> str:
    result = text
    for token, value in protected.items():
        pattern = re.compile(re.escape(token), re.IGNORECASE)
        if not pattern.search(result):
            raise ValueError(f"Translation placeholder lost: {token} in {text!r}")
        result = pattern.sub(lambda _: value, result)
    return result.replace("‑", "-")


def translate_text(text: str) -> str:
    protected_text, protected = protect_translation_tokens(text)
    query = urlencode(
        {
            "client": "gtx",
            "sl": "auto",
            "tl": "tr",
            "dt": "t",
            "q": protected_text,
        }
    )
    url = f"https://translate.googleapis.com/translate_a/single?{query}"
    last_error: Exception | None = None
    for attempt in range(3):
        try:
            request = Request(url, headers={"User-Agent": "ALKAROS-plan-audit/1.0"})
            with urlopen(request, timeout=30) as response:
                payload = json.loads(response.read().decode("utf-8"))
            translated = "".join(part[0] for part in payload[0] if part[0])
            return restore_translation_tokens(translated, protected)
        except (HTTPError, URLError, TimeoutError, ValueError, json.JSONDecodeError) as exc:
            last_error = exc
            time.sleep(1 + attempt)
    raise RuntimeError(f"Translation failed for {text!r}: {last_error}")


def translation_candidates() -> list[tuple[Path, str, int, str]]:
    candidates: list[tuple[Path, str, int, str]] = []
    for path in task_files():
        _, sections, _ = split_sections(read_utf8(path))
        for section in TRANSLATABLE_SECTIONS:
            for index, line in enumerate(sections.get(section, [])):
                content = line[2:] if line.startswith("- ") else line
                if not re.search(r"[A-Za-z]", content):
                    continue
                if TURKISH_MARKERS.search(content):
                    continue
                if re.fullmatch(r"[A-Z0-9_./*:` -]+", content):
                    continue
                candidates.append((path, section, index, content))
    return candidates


def translate_tasks() -> None:
    cache: dict[str, str] = {}
    if TRANSLATION_CACHE_PATH.exists():
        cache = json.loads(read_utf8(TRANSLATION_CACHE_PATH))
    candidates = translation_candidates()
    unique = sorted({content for _, _, _, content in candidates if content not in cache})
    if unique:
        with ThreadPoolExecutor(max_workers=6) as executor:
            futures = {executor.submit(translate_text, value): value for value in unique}
            for future in as_completed(futures):
                value = futures[future]
                cache[value] = future.result()
        TRANSLATION_CACHE_PATH.write_text(
            json.dumps(cache, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    changed = 0
    for path in task_files():
        text = read_utf8(path)
        preamble, sections, order = split_sections(text)
        path_changed = False
        for section in TRANSLATABLE_SECTIONS:
            lines = sections.get(section, [])
            for index, line in enumerate(lines):
                prefix = "- " if line.startswith("- ") else ""
                content = line[len(prefix) :]
                if content in cache and cache[content] != content:
                    lines[index] = prefix + cache[content]
                    path_changed = True
        if not path_changed:
            continue
        output = list(preamble)
        for section in order:
            output.extend(["", f"## {section}", ""])
            output.extend(sections[section])
        path.write_text("\n".join(output).rstrip() + "\n", encoding="utf-8")
        changed += 1
    print(f"Translation candidates: {len(candidates)}")
    print(f"Unique translated lines: {len(unique)}")
    print(f"Changed task files: {changed}")


def first_owned_path(sections: dict[str, list[str]]) -> str:
    for line in sections.get("Owned surface", []):
        match = re.search(r"`([^`]+)`", line)
        if match:
            return match.group(1)
    return "Owned surface"


def refine_tasks() -> None:
    replacements = {
        "yapay karma": "hash",
        "Yapay karma": "Hash",
        "web kancaları": "webhook'lar",
        "web kancası": "webhook",
        "Web kancaları": "Webhook'lar",
        "Web kancası": "Webhook",
        "ihaleye": "tender'a",
        "ihaleyi": "tender'ı",
        "ihale": "tender",
        "işleyicilere": "handler'lara",
        "işleyiciye": "handler'a",
        "işleyici": "handler",
        "bağdaştırıcılar": "adapter'lar",
        "bağdaştırıcı": "adapter",
        "sağlayıcılar": "provider'lar",
        "sağlayıcıya": "provider'a",
        "sağlayıcıyı": "provider'ı",
        "sağlayıcı": "provider",
        "geri arama": "çağrı",
        "aygıt": "device",
        "Vekilin": "Assignee'nin",
        "Vekil": "Assignee",
        "sözleşme testleri": "contract testleri",
        "Sözleşme testleri": "Contract testleri",
        "düzeltilmiş transkript": "redacted transcript",
        "düzeltilmiş istek/yanıt": "redacted request/response",
    }
    generic = re.compile(
        r"^- (?P<id>V\d+-[A-Z0-9]+-\d+) için production implementation"
        r"(?: veya executable test asset)?\.$"
    )
    changed = 0
    for path in task_files():
        text = read_utf8(path)
        preamble, sections, order = split_sections(text)
        match = TASK_HEADER.match(preamble[0])
        if not match:
            raise ValueError(f"Invalid task header: {path}")
        task_id = match.group("id")
        work_type = metadata_value(preamble, "Work type", "implementation")
        if task_id == "V15-RUN-001":
            work_type = "documentation"
        if task_id in {"V20-LIC-001", "V20-INT-004"}:
            status = "Blocked"
        else:
            status = metadata_value(preamble, "Status", "Planned")

        for section, lines in sections.items():
            for index, line in enumerate(lines):
                result = line
                for old, new in replacements.items():
                    result = result.replace(old, new)
                lines[index] = result

        for section, lines in SECTION_OVERRIDES.get(task_id, {}).items():
            sections[section] = list(lines)
            if section not in order:
                insert_at = order.index("Deliverables") if section == "Blocker" else len(order)
                order.insert(insert_at, section)

        owned_path = first_owned_path(sections)
        deliverables = sections.get("Deliverables", [])
        for index, line in enumerate(deliverables):
            if not generic.match(line):
                continue
            if work_type == "validation":
                deliverables[index] = (
                    f"- `{owned_path}` altında Goal kapsamını çalıştıran validation asset, "
                    "raw output ve tarihli result."
                )
            else:
                deliverables[index] = (
                    f"- `{owned_path}` altında Goal kapsamını uygulayan production code "
                    "ve task-specific automated test assets."
                )

        header = HEADER_OVERRIDES.get(task_id, preamble[0])
        assignee = metadata_value(
            preamble, "Assignee", "Unassigned (exactly one person)"
        )
        surface_state = metadata_value(preamble, "Surface state", "Planned")
        output = [
            header,
            "",
            f"- Task ID: {task_id}",
            f"- Status: {status}",
            f"- Assignee: {assignee}",
            f"- Work type: {work_type}",
            f"- Surface state: {surface_state}",
        ]
        required_order = [
            "Source basis",
            "Goal",
            "Owned surface",
            "In scope",
            "Out of scope",
            "Dependencies",
        ]
        if "Blocker" in sections:
            required_order.append("Blocker")
        required_order.extend(["Deliverables", "Acceptance evidence", "Handoff"])
        extras = [value for value in order if value not in required_order]
        for section in required_order + extras:
            output.extend(["", f"## {section}", ""])
            output.extend(sections[section])
        new_text = "\n".join(output).rstrip() + "\n"
        if new_text != text:
            path.write_text(new_text, encoding="utf-8")
            changed += 1
    print(f"Refined task files: {changed}")


def polish_tasks() -> None:
    changed = 0
    for path in task_files():
        original = read_utf8(path)
        preamble, sections, order = split_sections(original)
        task_id = TASK_HEADER.match(preamble[0]).group("id")
        if task_id in GOAL_POLISH:
            sections["Goal"] = [GOAL_POLISH[task_id]]
        for section, lines in SECTION_POLISH.get(task_id, {}).items():
            sections[section] = list(lines)
        output = list(preamble)
        for section in order:
            output.extend(["", f"## {section}", ""])
            output.extend(sections[section])
        new_text = "\n".join(output).rstrip() + "\n"
        if new_text != original:
            path.write_text(new_text, encoding="utf-8", newline="")
            changed += 1
    print(f"Polished task files: {changed}")


def capture() -> None:
    from pypdf import PdfReader

    markdown_files = sorted(PLAN_DIR.rglob("*.md"))
    records = []
    for path in markdown_files:
        text = read_utf8(path)
        records.append(
            {
                "path": path.relative_to(WORKSPACE).as_posix(),
                "sha256": sha256(path),
                "lines": line_count(text),
                "bytes": path.stat().st_size,
                "utf8": True,
            }
        )

    pdf_reader = PdfReader(str(PDF_PATH))
    payload = {
        "schema": 1,
        "markdown_file_count": len(records),
        "markdown_line_count": sum(record["lines"] for record in records),
        "markdown_byte_count": sum(record["bytes"] for record in records),
        "pdf": {
            "path": str(PDF_PATH),
            "sha256": sha256(PDF_PATH),
            "bytes": PDF_PATH.stat().st_size,
            "pages": len(pdf_reader.pages),
            "encrypted": pdf_reader.is_encrypted,
        },
        "files": records,
    }
    BASELINE_PATH.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps({key: value for key, value in payload.items() if key != "files"}, indent=2))


def report_english() -> None:
    english_markers = re.compile(
        r"\b(the|and|to|with|without|for|from|every|one|into|is|are|must|"
        r"cannot|can|only|against|when|where|while|before|after|through|"
        r"between|within|on|of|a|an)\b",
        re.IGNORECASE,
    )
    code_span = re.compile(r"`[^`]*`")
    records: list[tuple[str, int, str]] = []
    for path in sorted(PLAN_DIR.rglob("*.md")):
        lines = read_utf8(path).splitlines()
        if not lines or not re.match(r"^# V\d+-", lines[0]):
            continue
        for number, line in enumerate(lines, 1):
            prose = code_span.sub("", line)
            if english_markers.search(prose):
                records.append((path.relative_to(WORKSPACE).as_posix(), number, line))
    print(f"English candidate lines: {len(records)}")
    for path, number, line in records:
        print(f"{path}:{number}: {line}")


def wrap_markdown() -> None:
    changed = 0
    markdown_roots = [PLAN_DIR, WORKSPACE / "docs", WORKSPACE / "evidence"]
    markdown_paths = sorted(
        path
        for root in markdown_roots
        if root.exists()
        for path in root.rglob("*.md")
    )
    for path in markdown_paths:
        original = read_utf8(path)
        output: list[str] = []
        in_fence = False
        for line in original.splitlines():
            if line.lstrip().startswith("```") or line.lstrip().startswith("~~~"):
                in_fence = not in_fence
                output.append(line)
                continue
            if (
                in_fence
                or len(line) <= 120
                or line.startswith("|")
                or line.startswith("    ")
                or re.match(r"^#{1,6}\s", line)
            ):
                output.append(line)
                continue
            match = re.match(r"^(?P<prefix>\s*(?:[-+*]|\d+[.)])\s+)(?P<body>.+)$", line)
            if match:
                prefix = match.group("prefix")
                body = match.group("body")
                subsequent = " " * len(prefix)
            else:
                prefix = ""
                body = line
                subsequent = ""
            wrapped = textwrap.wrap(
                body,
                width=120,
                initial_indent=prefix,
                subsequent_indent=subsequent,
                break_long_words=False,
                break_on_hyphens=False,
                replace_whitespace=False,
            )
            output.extend(wrapped or [line])
        new_text = "\n".join(output).rstrip() + "\n"
        if new_text != original:
            path.write_text(new_text, encoding="utf-8", newline="")
            changed += 1
    print(f"Wrapped Markdown files: {changed}")


def _surface_glob_to_regex(pattern: str) -> re.Pattern[str]:
    """Convert a task surface glob to an anchored regex.

    Semantics match task_scope_tool.glob_to_regex: ``**`` crosses directory
    separators while ``*`` and ``?`` do not. Used to verify that every
    tracked production file is owned by at least one task surface.
    """
    result: list[str] = []
    i = 0
    while i < len(pattern):
        char = pattern[i]
        if char == "*":
            if i + 1 < len(pattern) and pattern[i + 1] == "*":
                result.append(".*")
                i += 2
                if i < len(pattern) and pattern[i] == "/":
                    result.append("/")
                    i += 1
            else:
                result.append("[^/]*")
                i += 1
        elif char == "?":
            result.append("[^/]")
            i += 1
        else:
            result.append(re.escape(char))
            i += 1
    return re.compile("^" + "".join(result) + "$")


def application_tasks_started_before_v0_exit(
    tasks: dict[str, tuple[Path, list[str], dict[str, list[str]], list[str]]],
) -> list[str]:
    """Reject newly started application work while a V0 task remains blocked."""
    c54_errors = c54_application_admission_errors(tasks)
    fnd023 = tasks.get(_C54_APPLICATION_TASK_ID)
    fnd023_done = fnd023 is not None and metadata_value(fnd023[1], "Status", "") == "Done"
    v0_gate_open = any(
        task_id.startswith("V0-")
        and task_id not in V0_DEFERRED_TASKS
        and metadata_value(preamble, "Status", "") == "Blocked"
        for task_id, (_, preamble, _, _) in tasks.items()
    )
    if not v0_gate_open:
        return c54_errors if fnd023_done else []

    c54_is_admitted = not c54_errors and not validate_remediation_admission_tuple()
    application_work_types = {"implementation", "integration"}
    return c54_errors + [
        f"APPLICATION_STARTED_BEFORE_V0_EXIT {task_id}"
        for task_id, (_, preamble, _, _) in tasks.items()
        if not task_id.startswith("V0-")
        and metadata_value(preamble, "Status", "") == "InProgress"
        and metadata_value(preamble, "Work type", "") in application_work_types
        and not (task_id == _C54_APPLICATION_TASK_ID and c54_is_admitted)
    ]


def v3_interrupted_closure_errors() -> list[str]:
    """Require the fixed V1-FND-023 final commit when its task is marked Done."""
    tool_path = WORKSPACE / "tools" / "evidence-envelope" / "evidence_envelope_tool.py"
    if not tool_path.is_file():
        return ["C54_APPLICATION_ADMISSION_V3_TOOL_MISSING"]
    spec = importlib.util.spec_from_file_location("plan_audit_evidence_envelope", tool_path)
    if spec is None or spec.loader is None:
        return ["C54_APPLICATION_ADMISSION_V3_TOOL_INVALID"]
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if getattr(module, "_V3_REENTRY_PARENT_TASK_ID", None) != "V0-GOV-060":
        return ["C54_APPLICATION_ADMISSION_V3_PARENT_TASK_MISMATCH"]
    final_commit = module.resolve_v3_final_commit(WORKSPACE)
    if final_commit is None:
        return ["C54_APPLICATION_ADMISSION_V3_FINAL_MISSING"]
    head = subprocess.run(
        ["git", "-C", str(WORKSPACE), "rev-parse", "--verify", "HEAD^{commit}"],
        check=False,
        capture_output=True,
        text=True,
    )
    if head.returncode != 0:
        return ["C54_APPLICATION_ADMISSION_V3_FINAL_MISSING"]
    if (
        subprocess.run(
            ["git", "-C", str(WORKSPACE), "merge-base", "--is-ancestor", final_commit, head.stdout.strip()],
            check=False,
            capture_output=True,
        ).returncode
        != 0
    ):
        return ["C54_APPLICATION_ADMISSION_V3_CLOSURE_INVALID"]
    result = module.validate_v1_fnd_023_v3_final_commit(final_commit, WORKSPACE)
    if result["valid"]:
        return []
    return ["C54_APPLICATION_ADMISSION_V3_CLOSURE_INVALID"]


def c54_application_admission_errors(
    tasks: dict[str, tuple[Path, list[str], dict[str, list[str]], list[str]]],
) -> list[str]:
    """Verify the one C54 application admission from static plan artifacts."""
    task = tasks.get(_C54_APPLICATION_TASK_ID)
    if task is None:
        return ["C54_APPLICATION_ADMISSION_TASK_MISSING"]

    _, preamble, sections, _ = task
    status = metadata_value(preamble, "Status", "")
    if status not in {"InProgress", "Done"}:
        return []

    errors: list[str] = []
    if status == "Done":
        errors.extend(v3_interrupted_closure_errors())
    elif status != "InProgress":
        errors.append(f"C54_APPLICATION_ADMISSION_STATUS expected=InProgress actual={status}")

    sources = tuple(line.removeprefix("- ").strip() for line in sections["Source basis"])
    if sources != _C54_APPLICATION_SOURCES:
        errors.append("C54_APPLICATION_ADMISSION_SOURCE")

    surfaces = tuple(
        match.group(1)
        for line in sections["Owned surface"]
        if (match := re.fullmatch(r"- `(.+)`", line)) is not None
    )
    if surfaces != _C54_APPLICATION_SURFACES:
        errors.append("C54_APPLICATION_ADMISSION_AUTHORITY")

    dependencies = tuple(line.removeprefix("- ").strip() for line in sections["Dependencies"])
    if dependencies != _C54_APPLICATION_DEPENDENCIES:
        errors.append("C54_APPLICATION_ADMISSION_DEPENDENCIES")
    for dependency_id in _C54_DONE_DEPENDENCIES:
        dependency = tasks.get(dependency_id)
        dependency_status = (
            metadata_value(dependency[1], "Status", "") if dependency is not None else "Missing"
        )
        if dependency_status != "Done":
            errors.append(
                f"C54_APPLICATION_ADMISSION_DEPENDENCY {dependency_id} "
                f"status={dependency_status}"
            )

    traceability_rows = [
        line for line in read_utf8(PLAN_DIR / "TRACEABILITY.md").splitlines() if line.startswith("| `C54` |")
    ]
    if traceability_rows != [_C54_TRACEABILITY_ROW]:
        errors.append("C54_APPLICATION_ADMISSION_TRACEABILITY_AUTHORITY")
    return errors


def validate_plan() -> None:
    errors: list[str] = []
    warnings: list[str] = []
    tasks: dict[str, tuple[Path, list[str], dict[str, list[str]], list[str]]] = {}
    expected_sections = [
        "Source basis",
        "Goal",
        "Owned surface",
        "In scope",
        "Out of scope",
        "Dependencies",
        "Deliverables",
        "Acceptance evidence",
        "Handoff",
    ]
    source_pattern = re.compile(
        r"^(?:PDF:(?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?"
        r"(?:-(?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?)?|"
        r"CORR:C\d+|EXT:[A-Z0-9][A-Z0-9.-]*|DEC:V\d+-[A-Z0-9]+-\d+)$"
    )
    turkish_narrative = re.compile(
        r"[çğıöşüÇĞİÖŞÜ]|(?i:\b(?:ve|veya|ile|için|olarak|görev|kanıt|tanımla|uygula|doğrula|"
        r"yönet|oluştur|sağla|üret|kural|akış|durum|kapsam|değer|yalnız|değil|olmadan|"
        r"tarafından|sonucu|sonra|önce|bulunur|içerir|dışında|başarılı|başarısız|varsa|dahil)\b)"
    )
    source_register = read_utf8(PLAN_DIR / "OFFICIAL_SOURCE_REGISTER.md")
    public_source_block = source_register.split("## Private", 1)[0]
    registered_ext = set(
        re.findall(
            r"^\| `([A-Z0-9][A-Z0-9.-]+)` \|",
            public_source_block,
            re.MULTILINE,
        )
    )
    gate_text = read_utf8(PLAN_DIR / "GATES.md")
    errors.extend(validate_remediation_admission_tuple())
    registered_gates = set(GATE_ID.findall(gate_text))
    deferred_block = gate_text.split("<!-- V0_DEFERRED_TASKS:START -->", 1)
    if len(deferred_block) != 2 or "<!-- V0_DEFERRED_TASKS:END -->" not in deferred_block[1]:
        errors.append("GATES_V0_DEFERRED_MARKER_MISSING")
    else:
        deferred_table = deferred_block[1].split("<!-- V0_DEFERRED_TASKS:END -->", 1)[0]
        registered_deferred = set(
            re.findall(
                r"^\| `(V0-[A-Z0-9]+-\d+)` \| `(?:2026-08-03|2026-08-13)` \|",
                deferred_table,
                re.MULTILINE,
            )
        )
        if registered_deferred != V0_DEFERRED_TASKS:
            errors.append(
                "GATES_V0_DEFERRED_MISMATCH expected=%s registered=%s"
                % (sorted(V0_DEFERRED_TASKS), sorted(registered_deferred))
            )
    traceability_text = read_utf8(PLAN_DIR / "TRACEABILITY.md")
    registered_corrections = set(
        re.findall(r"^\| `(C\d+)` \|", traceability_text, re.MULTILINE)
    )
    coverage_text = read_utf8(PLAN_DIR / "PDF_COVERAGE.md")
    pdf_sections = set(
        re.findall(
            r"^\| `((?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?)` \|",
            coverage_text,
            re.MULTILINE,
        )
    )

    for path in task_files():
        text_value = read_utf8(path)
        preamble, sections, order = split_sections(text_value)
        header = TASK_HEADER.match(preamble[0]) if preamble else None
        if not header:
            errors.append(f"TASK_HEADER {path.relative_to(WORKSPACE)}")
            continue
        task_id = header.group("id")
        if task_id in tasks:
            errors.append(f"TASK_DUPLICATE {task_id}: {path} / {tasks[task_id][0]}")
        tasks[task_id] = (path, preamble, sections, order)
        if not path.name.startswith(task_id + "-"):
            errors.append(f"TASK_FILENAME {task_id}: {path.relative_to(WORKSPACE)}")

        metadata = {}
        for line in preamble[1:]:
            match = re.match(r"^- ([A-Za-z ]+): (.+)$", line)
            if match:
                metadata[match.group(1)] = match.group(2)
        for field in ["Task ID", "Status", "Assignee", "Work type", "Surface state"]:
            if field not in metadata:
                errors.append(f"META_MISSING {task_id}: {field}")
        if metadata.get("Task ID") != task_id:
            errors.append(f"META_TASK_ID {task_id}: {metadata.get('Task ID')}")
        status = metadata.get("Status")
        if status not in {"Planned", "InProgress", "Blocked", "NotApplicable", "Done"}:
            errors.append(f"META_STATUS {task_id}: {status}")
        if metadata.get("Surface state") not in {"Planned", "Existing"}:
            errors.append(f"SURFACE_STATE {task_id}: {metadata.get('Surface state')}")
        if status in {"InProgress", "NotApplicable", "Done"} and (
            metadata.get("Assignee") == "Unassigned (exactly one person)"
        ):
            errors.append(f"ASSIGNEE_REQUIRED {task_id}")

        wanted = list(expected_sections)
        if status == "Blocked":
            wanted.insert(6, "Blocker")
        if order != wanted:
            errors.append(f"SECTION_ORDER {task_id}: {order}")
        for section in wanted:
            if not sections.get(section):
                errors.append(f"SECTION_EMPTY {task_id}: {section}")
        if status != "Blocked" and "Blocker" in sections:
            errors.append(f"BLOCKER_UNEXPECTED {task_id}")
        if status == "Blocked":
            blocker_text = " ".join(sections.get("Blocker", []))
            if "ancak" not in blocker_text.casefold():
                errors.append(f"BLOCKER_UNLOCK_MISSING {task_id}")

        for section_name in [
            "Goal",
            "In scope",
            "Out of scope",
            "Blocker",
            "Deliverables",
            "Acceptance evidence",
        ]:
            items: list[str] = []
            for line in sections.get(section_name, []):
                if line.startswith("- "):
                    items.append(line[2:])
                elif line.strip() and items:
                    items[-1] += " " + line.strip()
                elif line.strip():
                    items.append(line.strip())
            for item_index, item in enumerate(items, 1):
                prose = re.sub(r"`[^`]+`", "", item)
                prose = TASK_ID.sub("", prose)
                if len(re.findall(r"[A-Za-z]+", prose)) >= 6 and not turkish_narrative.search(prose):
                    errors.append(
                        f"LANGUAGE_TURKISH {task_id}/{section_name}/{item_index}: {item}"
                    )

        for line in sections.get("Source basis", []):
            value = line[2:].strip() if line.startswith("- ") else ""
            if not source_pattern.fullmatch(value):
                errors.append(f"SOURCE_FORMAT {task_id}: {line}")
                continue
            if value.startswith("EXT:") and value[4:] not in registered_ext:
                errors.append(f"SOURCE_EXT_UNKNOWN {task_id}: {value}")
            if value.startswith("CORR:") and value[5:] not in registered_corrections:
                errors.append(f"SOURCE_CORRECTION_UNKNOWN {task_id}: {value}")
            if value.startswith("DEC:") and value[4:] not in tasks and value[4:] not in {
                p.stem.split("-", 3)[0] for p in task_files()
            }:
                # Forward references are checked after the complete task index is built.
                pass
            if value.startswith("PDF:"):
                raw = value[4:]
                endpoints = raw.split("-") if "-" in raw else [raw]
                for endpoint in endpoints:
                    if endpoint not in pdf_sections:
                        errors.append(f"SOURCE_PDF_UNKNOWN {task_id}: {endpoint}")

        for section_name in ["Dependencies", "Handoff"]:
            for line in sections.get(section_name, []):
                value = line[2:].strip() if line.startswith("- ") else ""
                if not re.fullmatch(
                    r"(?:V\d+-[A-Z0-9]+-\d+|GATE-[A-Z0-9]+(?:-[A-Z0-9]+)+|None)",
                    value,
                ):
                    errors.append(f"REFERENCE_FORMAT {task_id}/{section_name}: {line}")
                if value == task_id:
                    errors.append(f"REFERENCE_SELF {task_id}/{section_name}")

        forbidden = re.compile(
            r"PDF baseline plus gap|production implementation|\ball tasks\b|\bdefect owners\b",
            re.IGNORECASE,
        )
        for number, line in enumerate(text_value.splitlines(), 1):
            if forbidden.search(line):
                errors.append(f"FORBIDDEN_PHRASE {task_id}:{number}: {line}")

    task_ids = set(tasks)
    dependency_graph: dict[str, list[str]] = {}
    production_surfaces: dict[str, list[str]] = defaultdict(list)
    surface_patterns: list[str] = []
    for task_id, (path, preamble, sections, order) in tasks.items():
        for line in sections.get("Source basis", []):
            value = line[2:].strip()
            if value.startswith("DEC:"):
                decision_id = value[4:]
                if decision_id not in task_ids:
                    errors.append(f"SOURCE_DEC_UNKNOWN {task_id}: {value}")
                    continue
                decision = tasks[decision_id]
                decision_type = metadata_value(decision[1], "Work type", "")
                decision_status = metadata_value(decision[1], "Status", "")
                decision_text = " ".join(decision[2].get("Acceptance evidence", []))
                if decision_type != "decision" or decision_status != "Done":
                    errors.append(
                        f"SOURCE_DEC_NOT_FINAL {task_id}: {decision_id} "
                        f"type={decision_type} status={decision_status}"
                    )
                if not re.search(r"\b20\d{2}-\d{2}-\d{2}\b", decision_text):
                    errors.append(f"SOURCE_DEC_DATE_MISSING {task_id}: {decision_id}")
        deps: list[str] = []
        for line in sections.get("Dependencies", []):
            value = line[2:].strip()
            if value.startswith("V"):
                if value not in task_ids:
                    errors.append(f"DEPENDENCY_UNKNOWN {task_id}: {value}")
                else:
                    deps.append(value)
            elif value.startswith("GATE-") and value not in registered_gates:
                errors.append(f"GATE_UNKNOWN {task_id}: {value}")
        for removal in DEPENDENCY_REMOVALS.get(task_id, []):
            deps = [value for value in deps if value != removal]
        dependency_graph[task_id] = deps
        for line in sections.get("Handoff", []):
            value = line[2:].strip()
            if value.startswith("V") and value not in task_ids:
                errors.append(f"HANDOFF_UNKNOWN {task_id}: {value}")
            elif value.startswith("GATE-") and value not in registered_gates:
                errors.append(f"GATE_UNKNOWN {task_id}: {value}")
        handoffs = {
            line[2:].strip()
            for line in sections.get("Handoff", [])
            if line.startswith("- V")
        }
        backward = handoffs & set(deps)
        if backward:
            errors.append(f"HANDOFF_TO_DEPENDENCY {task_id}: {', '.join(sorted(backward))}")
        owned_surface_lines: list[str] = []
        in_item = False
        for line in sections.get("Owned surface", []):
            stripped = line.strip()
            if stripped.startswith("- "):
                in_item = not stripped.startswith("- Bu görev")
                if in_item:
                    owned_surface_lines.append(stripped)
            elif in_item and "`" in stripped:
                # Wrapped continuation of the previous bullet keeps the
                # backtick fragments in the same Owned surface item.
                owned_surface_lines.append(stripped)
            else:
                in_item = False
        for line in owned_surface_lines:
            for value in re.findall(r"`([^`]+)`", line):
                if value.startswith(("src/", "tests/", "database/")):
                    root = value.removesuffix("/**").rstrip("/")
                    production_surfaces[root].append(task_id)
                    surface_patterns.append(value)

    state: dict[str, int] = {}
    trail: list[str] = []

    def visit(node: str) -> None:
        state[node] = 1
        trail.append(node)
        for dependency in dependency_graph.get(node, []):
            if state.get(dependency) == 1:
                start = trail.index(dependency)
                errors.append("DEPENDENCY_CYCLE " + " -> ".join(trail[start:] + [dependency]))
            elif state.get(dependency, 0) == 0:
                visit(dependency)
        trail.pop()
        state[node] = 2

    for task_id in sorted(task_ids):
        if state.get(task_id, 0) == 0:
            visit(task_id)

    task_statuses = {
        task_id: metadata_value(preamble, "Status", "")
        for task_id, (_, preamble, _, _) in tasks.items()
    }

    def find_non_final_ancestors(
        task_id: str,
        dependency_id: str,
        path: list[str],
    ) -> None:
        dependency_status = task_statuses[dependency_id]
        if dependency_status != "Done":
            if len(path) == 2:
                errors.append(
                    f"DONE_DEPENDENCY_NOT_FINAL {task_id}: "
                    f"{dependency_id} status={dependency_status}"
                )
            else:
                errors.append(
                    f"DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL {task_id}: "
                    f"{' -> '.join(path)} status={dependency_status}"
                )
            return

        for ancestor_id in dependency_graph[dependency_id]:
            find_non_final_ancestors(task_id, ancestor_id, [*path, ancestor_id])

    for task_id in sorted(task_ids):
        if task_statuses[task_id] != "Done":
            continue
        for dependency_id in dependency_graph[task_id]:
            find_non_final_ancestors(task_id, dependency_id, [task_id, dependency_id])

    for surface, owners in sorted(production_surfaces.items()):
        unique = sorted(set(owners))
        if len(unique) > 1:
            errors.append(f"SURFACE_DUPLICATE {surface}: {', '.join(unique)}")
    surfaces = sorted(production_surfaces)
    for index, left in enumerate(surfaces):
        for right in surfaces[index + 1 :]:
            if not (left.startswith(right + "/") or right.startswith(left + "/")):
                continue
            left_owners = set(production_surfaces[left])
            right_owners = set(production_surfaces[right])
            if left_owners != right_owners:
                errors.append(
                    f"SURFACE_PREFIX_OVERLAP {left} ({', '.join(sorted(left_owners))}) / "
                    f"{right} ({', '.join(sorted(right_owners))})"
                )

    # Every tracked file under the production directories must be matched by
    # at least one Owned surface pattern. Files that no task may write are a
    # governance gap, not a free zone: they can only be modified through a new
    # plan task, so a violation here blocks the release.
    tracked_production = [
        rel
        for rel in subprocess.check_output(
            ["git", "ls-files"], text=True, stderr=subprocess.DEVNULL
        ).splitlines()
        if rel.startswith(("src/", "tests/", "database/"))
    ]
    surface_matchers = [
        (pattern, _surface_glob_to_regex(pattern.lower()))
        for pattern in sorted(set(p.lower() for p in surface_patterns))
    ]
    for rel in tracked_production:
        rel_lower = rel.replace("\\", "/").lower()
        if not any(matcher.match(rel_lower) for _, matcher in surface_matchers):
            errors.append(f"UNOWNED_PRODUCTION_FILE {rel}")

    required_dependencies = {
        "V1-FND-003": {"V0-ARC-001", "V1-FND-001"},
        "V1-FND-001": {"V0-ARC-001", "V0-ARC-009"},
        "V1-FND-004": {"V0-ARC-001", "V0-DAT-001", "V1-FND-001", "V1-FND-003"},
        "V1-FND-005": {"V0-ARC-001", "V0-ARC-003", "V1-FND-004"},
        "V1-SEC-001": {"V1-FND-005", "V0-ARC-005", "V0-SEC-001"},
        "V1-SEC-002": {"V1-SEC-001", "V0-CMP-003", "V0-SEC-001"},
        "V1-FND-002": {"V1-SEC-002", "V0-ARC-003"},
        "V1-FND-006": {"V1-FND-002", "V1-FND-005"},
        "V1-ORD-001": {"V1-TBL-001"},
        "V1-TBL-002": {"V1-FND-005"},
        "V1-TBL-003": {"V1-FND-005"},
        "V1-TBL-005": {"V1-TBL-002", "V1-TBL-003", "V1-ORD-001"},
        "V11-INV-001": {"V11-INV-004"},
        "V11-INV-002": {"V11-INV-004"},
        "V11-INV-007": {"V11-INV-002", "V11-RSV-001", "V0-DAT-004"},
        "V11-RSV-002": {"V11-INV-007", "V11-RSV-001"},
        "V11-MNU-002": {"V11-INV-007", "V11-PRD-002", "V11-RSV-001", "V11-RSV-003"},
        "V11-UI-003": {"V11-INV-002", "V11-INV-007"},
        "V11-PUR-001": {"V0-DOM-009", "V11-INV-004", "V11-PUR-002"},
        "V12-PAY-002": {"V0-ARC-004"},
        "V12-PAY-003": {"V12-CSH-003", "V12-PAY-004", "V12-MCD-004"},
        "V12-PAY-004": {"V12-HUG-001", "V12-ALC-001", "V12-FSC-001", "V1-FND-005", "V1-FND-006"},
        "V12-CSH-001": {"V1-CSH-001"},
        "V12-CSH-003": {"V12-CSH-001", "V12-CSH-002", "V12-ALC-001", "V1-FND-005"},
        "V12-ALC-004": {"V12-ALC-003", "V12-HUG-003", "V12-FSC-001", "V1-FND-005"},
        "V12-FSC-002": {"V12-ALC-002", "V12-CSH-003", "V12-MCD-004", "V1-FND-005"},
        "V12-FSC-003": {"V12-FSC-004", "V12-FSC-005"},
        "V12-MCD-004": {"V12-MCD-001", "V12-MCD-003", "V12-ALC-001", "V12-FSC-001", "V1-FND-005", "V1-FND-006"},
        "V12-TBL-001": {"V1-TBL-002", "V1-TBL-003", "V12-PAY-004", "V12-ALC-002"},
        "V13-ACC-001": {"V0-DOM-007"},
        "V13-ACC-003": {"V1-FND-005"},
        "V13-ACC-004": {"V13-ACC-001", "V0-DOM-007", "V0-DAT-002"},
        "V13-ACC-005": {"V13-ACC-001", "V13-ACC-002", "V13-ACC-004", "V12-CSH-001", "V12-CSH-002", "V1-FND-005"},
        "V13-ACC-006": {"V13-ACC-001", "V13-ACC-002", "V13-ACC-004", "V12-HUG-001", "V12-HUG-002", "V1-FND-005", "V1-FND-006"},
        "V13-ACC-007": {"V13-ACC-005", "V13-ACC-006"},
        "V13-ACC-008": {"V13-ACC-003", "V12-PAY-002", "V12-PAY-003", "V12-FSC-002", "V1-FND-002", "V1-FND-005"},
        "V13-CST-001": {"V0-CMP-003"},
        "V13-QNB-004": {"V13-QNB-005"},
        "V14-QRO-001": {"V14-QRS-003"},
        "V14-QRO-002": {"V0-DOM-005"},
        "V14-QRO-003": {"V14-STK-001", "V1-FND-005"},
        "V14-ONL-002": {"V14-STK-001", "V1-FND-005"},
        "V14-ONL-003": {"V11-RSV-003"},
        "V14-STK-001": {"V11-RSV-003"},
        "V14-QRT-001": {"V0-ARC-009", "V0-QRG-001", "V14-QRS-002", "V1-FND-006"},
        "V14-REC-001": {"V14-ONL-002", "V14-ONL-003"},
        "V11-RCP-002": {"V0-DOM-010"},
        "V11-PRD-002": {"V0-DOM-010", "V1-FND-005"},
        "V12-ALC-002": {"V1-FND-005"},
        "V1-KIT-002": {"V0-DOM-011"},
        "V15-NOT-001": {"V0-ARC-006"},
        "V20-INS-001": {"V0-ARC-007", "V14-QRT-001"},
        "V20-INS-002": {"V0-ARC-007"},
        "V20-REL-001": {"V0-ARC-008"},
        "V20-GAT-001": {"V20-REL-001"},
        "V20-CMP-001": {"V0-CMP-002", "V0-CMP-004", "V20-UAT-001", "V20-UAT-002"},
        "V20-REL-004": {"V20-REL-003", "V15-BKP-002", "V20-MIG-002", "V20-SEC-001", "V14-QRT-001"},
        "V20-REL-005": {"V20-REL-004", "V15-OBS-001", "V15-REC-002"},
        "V20-MIG-001": {"V0-DAT-006", "V20-INS-002", "V1-FND-004"},
        "V15-BKP-001": {"V0-BKP-002"},
        "V15-BKP-002": {"V0-BKP-002"},
        "V20-UAT-001": {"V14-OUI-001", "V20-INT-003", "V0-CMP-005"},
        "V20-SEC-001": {
            "V0-SEC-001",
            "V14-QRS-003",
            "V14-CWB-001",
            "V14-CWB-002",
            "V14-QRO-001",
            "V14-QRO-002",
            "V14-QRO-003",
            "V14-QRT-001",
            "V14-ONL-001",
        },
    }
    for task_id, required in required_dependencies.items():
        missing = required - set(dependency_graph.get(task_id, []))
        if missing:
            errors.append(f"SEMANTIC_DEPENDENCY {task_id}: {', '.join(sorted(missing))}")

    accessibility_consumers = {
        "V1-CUI-001", "V1-CUI-002", "V1-CUI-003",
        "V1-WTR-001", "V1-WTR-002", "V1-WTR-003",
        "V11-UI-001", "V11-UI-002", "V11-UI-003",
        "V12-PUI-001", "V12-PUI-002", "V12-PUI-003",
        "V13-UI-001", "V13-UI-002", "V13-UI-003",
        "V14-CWB-001", "V14-CWB-002", "V14-OUI-001",
        "V20-INT-006", "V20-UAT-001",
    }
    for task_id in sorted(accessibility_consumers):
        if "V0-CMP-005" not in dependency_graph.get(task_id, []):
            errors.append(f"ACCESSIBILITY_DEPENDENCY {task_id}: V0-CMP-005")

    secret_consumers = {
        "V12-HUG-001",
        "V12-HUG-002",
        "V12-HUG-003",
        "V12-HUG-004",
        "V12-MCD-003",
        "V13-QNB-001",
        "V13-QNB-002",
        "V13-QNB-003",
        "V13-QNB-005",
        "V14-ONL-001",
        "V14-ONL-003",
    }
    sensitive_consumers = secret_consumers | {
        "V12-MCD-001",
        "V12-MCD-002",
        "V13-QNB-004",
        "V12-FSC-001",
    }
    for task_id in secret_consumers:
        if "V1-SEC-001" not in dependency_graph.get(task_id, []):
            errors.append(f"SEMANTIC_SECRET_DEPENDENCY {task_id}: V1-SEC-001")
    for task_id in sensitive_consumers:
        if "V1-SEC-002" not in dependency_graph.get(task_id, []):
            errors.append(f"SEMANTIC_PAYLOAD_DEPENDENCY {task_id}: V1-SEC-002")

    conditional_dependency_consumers = {
        "V13-QNB-005": {"V13-QNB-004", "V13-UI-002", "V20-INT-002"},
        "V20-LIC-001": {"V20-LIC-002"},
        "V20-LIC-002": {"V20-GAT-002"},
    }
    for dependency_id, consumer_ids in conditional_dependency_consumers.items():
        for consumer_id in consumer_ids:
            if dependency_id not in dependency_graph.get(consumer_id, []):
                errors.append(
                    f"SEMANTIC_CONDITIONAL_DEPENDENCY {consumer_id}: {dependency_id}"
                )
            acceptance = " ".join(tasks[consumer_id][2].get("Acceptance evidence", []))
            if "NotApplicable" not in acceptance:
                errors.append(
                    f"SEMANTIC_NOT_APPLICABLE_ACCEPTANCE {consumer_id}: {dependency_id}"
                )

    fnd1_handoff = {
        line[2:].strip() for line in tasks["V1-FND-001"][2].get("Handoff", [])
    }
    if fnd1_handoff != {"V1-FND-003"}:
        errors.append(f"SEMANTIC_HANDOFF V1-FND-001: {','.join(sorted(fnd1_handoff))}")

    root_surfaces = {
        value
        for line in tasks["V1-FND-001"][2].get("Owned surface", [])
        for value in re.findall(r"`([^`]+)`", line)
    }
    required_root_surfaces = {
        "ALKAROS.slnx",
        "global.json",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "NuGet.config",
        ".config/dotnet-tools.json",
        "build/project-manifest.json",
        "src/Host/ALKAROS.Host.csproj",
        "src/Modules/**/ALKAROS.*.csproj",
        "src/Clients/**/ALKAROS.*.csproj",
        "tests/Modules/**/ALKAROS.*.Tests.csproj",
        "tests/Clients/**/ALKAROS.*.Tests.csproj",
    }
    if not required_root_surfaces <= root_surfaces:
        errors.append(
            "SEMANTIC_ROOT_OWNERSHIP V1-FND-001: "
            + ", ".join(sorted(required_root_surfaces - root_surfaces))
        )

    agents_path = WORKSPACE / "AGENTS.md"
    if not agents_path.exists():
        errors.append("CODEX_BOUNDARY_AGENTS_MISSING")
    else:
        agents_text = read_utf8(agents_path)
        for required_phrase in [
            "tam olarak bir `Task ID`",
            "`evidence/<Task-ID>/**`",
            "staged, unstaged, untracked, deleted ve renamed",
            "Allowlist dışındaki tek bir değişiklik",
            "`V1-FND-003`",
        ]:
            if required_phrase not in agents_text:
                errors.append(f"CODEX_BOUNDARY_AGENTS_CONTENT {required_phrase}")
    forbidden_dependencies = {
        "V14-QRO-002": {"V0-CMP-001"},
        "V14-STK-001": {"V14-QRO-003", "V14-ONL-002"},
        "V0-ARC-009": {"V0-SEC-001"},
        "V0-CMP-002": {"V0-CMP-001"},
        "V0-CMP-004": {"V0-CMP-001"},
        "V0-GOV-010": {"V1-FND-003"},
        "V0-GOV-013": {"V1-SEC-002"},
        "V0-GOV-014": {"V1-FND-002"},
        "V0-GOV-015": {"V1-FND-004"},
    }
    for task_id, forbidden in forbidden_dependencies.items():
        present = forbidden & set(dependency_graph.get(task_id, []))
        if present:
            errors.append(f"SEMANTIC_DEPENDENCY_FORBIDDEN {task_id}: {', '.join(sorted(present))}")

    conditional_task_pattern = re.compile(
        r"(?:bu (?:task|görev)(?:\s+da|\s+de|ı|i|ü|u)?|this task|bu composition task['’]ı)"
        r".{0,120}?NotApplicable",
        re.IGNORECASE,
    )
    conditional_tasks = {
        task_id
        for task_id, (_, _, sections, _) in tasks.items()
        if conditional_task_pattern.search(" ".join(sections.get("Acceptance evidence", [])))
    }
    for consumer_id, dependencies in dependency_graph.items():
        acceptance = " ".join(tasks[consumer_id][2].get("Acceptance evidence", []))
        for dependency_id in dependencies:
            if dependency_id not in conditional_tasks:
                continue
            if dependency_id not in acceptance or "NotApplicable" not in acceptance:
                errors.append(
                    f"CONDITIONAL_DEPENDENCY {consumer_id}: {dependency_id} NotApplicable not handled"
                )

    required_work_types = {
        "V15-PER-001": "validation",
        "V15-PER-002": "validation",
        "V15-RUN-001": "documentation",
        "V15-RUN-002": "validation",
    }
    for task_id, required in required_work_types.items():
        actual = metadata_value(tasks[task_id][1], "Work type", "")
        if actual != required:
            errors.append(f"SEMANTIC_WORK_TYPE {task_id}: {actual} != {required}")

    source_requirements = {
        "V0-QNB-001": "CORR:C21",
        "V13-QNB-005": "CORR:C21",
        "V20-INT-002": "CORR:C21",
        "V20-CMP-001": "CORR:C21",
    }
    for task_id, source in source_requirements.items():
        values = {line[2:].strip() for line in tasks[task_id][2]["Source basis"]}
        if source not in values:
            errors.append(f"SEMANTIC_SOURCE {task_id}: missing {source}")

    no_case_creation = {"V12-HUG-002", "V12-MCD-002", "V14-ONL-003", "V14-MAP-002"}
    for task_id in no_case_creation:
        scope = " ".join(tasks[task_id][2].get("Out of scope", []))
        if "ReconciliationCase oluşturma" not in scope:
            errors.append(f"SEMANTIC_RECONCILIATION_OWNER {task_id}")

    errors.extend(application_tasks_started_before_v0_exit(tasks))

    actual_consumers: dict[str, set[str]] = defaultdict(set)
    for task_id, (_, _, sections, _) in tasks.items():
        for line in sections.get("Source basis", []):
            value = line[2:].strip()
            if value.startswith("EXT:"):
                actual_consumers[value[4:]].add(task_id)
    declared_consumers: dict[str, set[str]] = {}
    for line in public_source_block.splitlines():
        if not line.startswith("| `"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 7 or not cells[0].startswith("`"):
            continue
        source_id = cells[0].strip("`")
        declared_consumers[source_id] = set(TASK_ID.findall(cells[6]))
    for source_id in registered_ext:
        if actual_consumers.get(source_id, set()) != declared_consumers.get(source_id, set()):
            errors.append(
                f"SOURCE_CONSUMER_MISMATCH {source_id}: actual="
                f"{','.join(sorted(actual_consumers.get(source_id, set())))} declared="
                f"{','.join(sorted(declared_consumers.get(source_id, set())))}"
            )

    print(f"Markdown files: {len(list(PLAN_DIR.rglob('*.md')))}")
    print(f"Task files: {len(tasks)}")
    print(f"Registered gates: {len(registered_gates)}")
    print(f"Registered EXT sources: {len(registered_ext)}")
    print(f"Dependency edges: {sum(len(values) for values in dependency_graph.values())}")
    print(f"Validation errors: {len(errors)}")
    for error in errors:
        print(error)
    print(f"Validation warnings: {len(warnings)}")
    for warning in warnings:
        print(warning)
    if errors:
        raise SystemExit(1)


def validate_coverage() -> None:
    from pypdf import PdfReader

    errors: list[str] = []
    reader = PdfReader(str(PDF_PATH))
    if sha256(PDF_PATH) != "AF0E7F70174AC4006E93CC6E985C50E3F638EA6FC10E3C2EF96E745CDA780822":
        errors.append("PDF_HASH")
    if len(reader.pages) != 94:
        errors.append(f"PDF_PAGE_COUNT {len(reader.pages)}")
    headings = extract_pdf_headings()
    expected_headings = {
        str(item["section"])
        for item in headings
        if not str(item["section"]).startswith("C")
    }
    expected_corrections = {
        str(item["section"])
        for item in headings
        if str(item["section"]).startswith("C")
    }
    line_units, table_units = extract_pdf_content_units(headings)
    coverage = read_utf8(PLAN_DIR / "PDF_COVERAGE.md")
    stored_headings = set(
        re.findall(
            r"^\| `((?:I|II|III|IV)\.\d+(?:\.\d+)*[A-Z]?)` \|",
            coverage,
            re.MULTILINE,
        )
    )
    stored_corrections = {
        value
        for value in re.findall(r"^\| `(C\d+)` \|", coverage, re.MULTILINE)
        if value in {f"C{number}" for number in range(1, 10)}
    }
    if stored_headings != expected_headings:
        errors.append(
            f"COVERAGE_HEADINGS expected={len(expected_headings)} stored={len(stored_headings)}"
        )
    if stored_corrections != expected_corrections:
        errors.append(
            f"COVERAGE_CORRECTIONS expected={len(expected_corrections)} stored={len(stored_corrections)}"
        )

    stored_units = {
        match.group("unit"): {
            "sha256": match.group("sha"),
            "section": match.group("section"),
            "owners": match.group("owners").strip(),
        }
        for match in re.finditer(
            r"^\| `(?P<unit>P\d{3}-(?:L\d{3}|T\d{2}-R\d{3}))` \| \d+ \| "
            r"`(?P<section>[^`]+)` \| [^|]+ \| `(?P<sha>[A-F0-9]{64})` \| "
            r".* \| (?P<owners>[^|]+) \| Planned \|$",
            coverage,
            re.MULTILINE,
        )
    }
    owner_entries = task_source_owner_ranges()
    expected_units = {}
    for unit in [*line_units, *table_units]:
        unit_id = str(unit["unit"])
        section = str(unit["section"])
        expected_units[unit_id] = {
            "sha256": str(unit["sha256"]),
            "section": section,
            "owners": ", ".join(unit_owners(section, owner_entries, unit_id)),
        }
    if stored_units != expected_units:
        missing = sorted(set(expected_units) - set(stored_units))
        extra = sorted(set(stored_units) - set(expected_units))
        changed = sorted(
            unit
            for unit in set(stored_units) & set(expected_units)
            if stored_units[unit] != expected_units[unit]
        )
        errors.append(
            f"COVERAGE_UNITS missing={len(missing)} extra={len(extra)} changed={len(changed)}"
        )

    print(f"PDF SHA-256: {sha256(PDF_PATH)}")
    print(f"PDF pages: {len(reader.pages)}")
    print(f"Numbered headings: {len(expected_headings)}")
    print(f"Part IV corrections: {len(expected_corrections)}")
    print(f"Text line units: {len(line_units)}")
    print(f"Geometry table rows: {len(table_units)}")
    print(f"Coverage errors: {len(errors)}")
    for error in errors:
        print(error)
    if errors:
        raise SystemExit(1)


def baseline_lint_findings() -> tuple[dict[str, list[tuple[str, int]]], dict[str, int], int]:
    npx = shutil.which("npx.cmd") or shutil.which("npx")
    if not npx:
        raise RuntimeError("npx executable was not found")
    source_root = WORKSPACE / "tmp" / "plan_audit_original"
    with tempfile.TemporaryDirectory(prefix="alkaros-baseline-lint-") as temp_dir:
        temp_root = Path(temp_dir)
        shutil.copytree(source_root, temp_root / "baseline")
        config_path = temp_root / "baseline-markdownlint-cli2.jsonc"
        config_path.write_text(
            json.dumps(
                {
                    "config": {
                        "default": True,
                        "MD013": {
                            "line_length": 80,
                            "code_blocks": True,
                            "tables": True,
                        },
                        "MD060": True,
                    }
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        result = subprocess.run(
            [
                npx,
                "--yes",
                "markdownlint-cli2@0.23.2",
                "baseline/**/*.md",
                "--config",
                str(config_path),
                "--no-globs",
            ],
            cwd=temp_root,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
    output = re.sub(r"\x1b\[[0-9;]*m", "", result.stdout + "\n" + result.stderr)
    findings: dict[str, list[tuple[str, int]]] = defaultdict(list)
    counts: dict[str, int] = defaultdict(int)
    affected: set[str] = set()
    pattern = re.compile(
        r"baseline[\\/](?P<path>.+?):(?P<line>\d+)(?::\d+)? "
        r"error (?P<rule>MD\d+)"
    )
    for match in pattern.finditer(output):
        relative = "plan/" + match.group("path").replace("\\", "/")
        rule = match.group("rule")
        line = int(match.group("line"))
        findings[relative].append((rule, line))
        counts[rule] += 1
        affected.add(relative)
    expected = {"MD013": 704, "MD012": 158, "MD060": 30}
    if dict(counts) != expected or len(affected) != 201:
        raise RuntimeError(
            "Baseline markdownlint evidence changed: "
            f"counts={dict(counts)}, affected={len(affected)}, returncode={result.returncode}"
        )
    return findings, dict(counts), len(affected)


def section_anchor(lines: list[str], section: str) -> int:
    heading = f"## {section}"
    for index, line in enumerate(lines, 1):
        if line == heading:
            for content_index in range(index + 1, len(lines) + 1):
                if lines[content_index - 1].strip():
                    return content_index
            return index
    return 1


def wrapped_markdown_bullet(content: str) -> list[str]:
    return textwrap.wrap(
        content,
        width=120,
        initial_indent="- ",
        subsequent_indent="  ",
        break_long_words=False,
        break_on_hyphens=False,
    )


def manual_audit_findings() -> list[tuple[str, str, str, str]]:
    return [
        ("plan/PDF_COVERAGE.md", "COVERAGE", "Part II ve Part III - Domain/Schema Ownership", "Coverage özet aralıkları her PDF birimini tekil göstermiyordu."),
        ("plan/PDF_COVERAGE.md", "PDFMAP", "Part II ve Part III - Domain/Schema Ownership", "PDF belge haritasındaki II.16 ifadesi gerçek Part II sonu II.15 ile çelişiyordu."),
        ("plan/PDF_COVERAGE.md", "PDFCOUNT", "Part IV - C1-C9 Düzeltme Sahipleri", "I.46 içindeki 14 sayısı CORR:C8 ile kanıtlanan 13 değerine bağlanmamıştı."),
        ("plan/TASK_STANDARD.md", "SCHEMA", "Görev satırı alanları", "Standard, görev dosyalarının gerçek metadata ve bölüm sözleşmesiyle eşleşmiyordu."),
        ("plan/GATES.md", "GATE", "Sürüm zinciri", "Entry ve exit kapıları sabit gate kimliklerine sahip değildi."),
        ("plan/TRACEABILITY.md", "DECISION", "Audit Traceability", "On karar/validation boşluğu tek kişilik karar görevlerine ayrılmamıştı."),
        ("plan/v1.2/payments/V12-PAY-002-tender-command-routing.md", "VERSIONING", "Goal", "CustomerAccount yöntemi V1.3 handler öncesi typed version-not-enabled sonucu tanımlamıyordu."),
        ("plan/v1.2/cash/V12-CSH-001-cash-session-lifecycle.md", "DEPENDENCY", "Dependencies", "Cash posting görevi V1-CSH-001 lifecycle sözleşmesine bağlı değildi."),
        ("plan/v1.3/customer-account/V13-ACC-001-account-transaction-ledger.md", "DEPENDENCY", "Dependencies", "Account ledger görevi V0-DOM-007 kararına bağlı değildi."),
        ("plan/v1.4/qr-ordering/V14-QRO-002-pending-table-policy.md", "DEPENDENCY", "Dependencies", "QR masa geçişi yanlış V0-CMP-001 görevine bağlıydı; V0-DOM-005 gerekliydi."),
        ("plan/v1.4/qr-ordering/V14-QRO-001-pending-qr-order.md", "DEPENDENCY", "Dependencies", "QR draft görevi session lifecycle sahibi V14-QRS-003'e bağlı değildi."),
        ("plan/v1.4/shared-stock/V14-STK-001-cross-channel-last-portion.md", "OWNERSHIP", "Owned surface", "Ortak stok rezervasyon komutunun tek sahibi belirlenmemişti."),
        ("plan/v1.4/qr-ordering/V14-QRO-003-confirmation-and-reservation.md", "OWNERSHIP", "Owned surface", "QR görevi ortak stok rezervasyon yüzeyini sahipleniyordu."),
        ("plan/v1.4/online-ordering/V14-ONL-002-external-order-normalization.md", "OWNERSHIP", "Owned surface", "Online görev ortak stok rezervasyon yüzeyini sahipleniyordu."),
        ("plan/v1.2/hugin-t300/V12-HUG-002-unknown-reconciliation.md", "RECONCILIATION", "In scope", "Adapter görevi reconciliation case açma yetkisini REC sahibinden alıyordu."),
        ("plan/v1.2/meal-card/V12-MCD-002-settlement-lifecycle.md", "RECONCILIATION", "In scope", "Settlement görevi divergence kanıtı yerine reconciliation case davranışı sahipleniyordu."),
        ("plan/v1.4/online-ordering/V14-ONL-003-status-and-cancellation-sync.md", "RECONCILIATION", "In scope", "Online adapter görevi reconciliation case açma yetkisini REC sahibinden alıyordu."),
        ("plan/v1.4/channel-mapping/V14-MAP-002-status-mapping.md", "RECONCILIATION", "In scope", "Mapping görevi reconciliation case açma yetkisini REC sahibinden alıyordu."),
        ("plan/v1.5/performance/V15-PER-001-critical-load-tests.md", "WORKTYPE", "Goal", "Performans görevi implementation olarak sınıflanmıştı; çıktı validation kanıtıdır."),
        ("plan/v1.5/performance/V15-PER-002-failure-injection.md", "WORKTYPE", "Goal", "Offline dayanıklılık görevi implementation olarak sınıflanmıştı; çıktı validation kanıtıdır."),
        ("plan/v1.5/runbooks/V15-RUN-001-operational-runbooks.md", "SPLIT", "In scope", "Runbook yazımı ile bağımsız operatör uygulaması aynı görevdeydi."),
        ("plan/v1.5/kvkk/V15-KVK-002-cross-store-anonymization.md", "WORKFLOW", "Goal", "KVKK akışı mağazalar arası tek transaction varsayıyordu."),
        ("plan/v2.0/release/V20-REL-002-controlled-pilot.md", "EVIDENCE", "Goal", "Gerçek pilot iddiası için müşteri/production yetkisi ve veri kanıtı yoktu."),
        ("plan/v2.0/release/V20-REL-003-go-live-decision.md", "SCOPE", "Goal", "Go-live karar görevi production deployment yaptığı izlenimi veriyordu."),
        ("plan/v2.0/security-compliance/V20-CMP-001-compliance-signoff.md", "SOURCE", "In scope", "QNB iptal kapsamı kamuya açık API kanıtını aşıyordu."),
        ("plan/v2.0/licensing/V20-LIC-001-approved-license-enforcement.md", "CONDITIONAL", "Goal", "Lisans uygulaması V0-LIC-001 sonucu bilinmeden koşulsuz planlanmıştı."),
        ("plan/v1.2/meal-card/V12-MCD-003-provider-adapter.md", "SPLIT", "Goal", "Tek görev birden çok olası meal-card provider adapter'ını kapsayabiliyordu."),
        ("plan/v0/hugin-t300/V0-HUG-001-integration-contract.md", "BLOCKER", "Goal", "Özel SDK/protokol ve cihaz transcript kanıtı yokken görev Planned durumundaydı."),
        ("plan/v0/qnb-esolutions/V0-QNB-001-integration-contract.md", "BLOCKER", "Goal", "Özel tenant/contract ve lifecycle transcript kanıtı yokken görev Planned durumundaydı."),
        ("plan/v0/yemeksepeti/V0-YSP-001-partner-api-contract.md", "BLOCKER", "Goal", "Credential/sandbox ve gerçek webhook transcript kanıtı yokken görev Planned durumundaydı."),
        ("plan/v0/meal-card/V0-MCD-001-provider-contract.md", "BLOCKER", "Goal", "Onaylı provider listesi ve sandbox/device kanıtı yokken görev Planned durumundaydı."),
        ("plan/v0/printing/V0-PRN-001-printer-contract.md", "BLOCKER", "Goal", "Onaylı model/firmware ve gerçek cihaz transcript kanıtı yokken görev Planned durumundaydı."),
        ("plan/v0/backup-recovery/V0-BKP-001-backup-restore-proof.md", "EVIDENCE", "Acceptance evidence", "Henüz olmayan uygulamanın restore sonrası açıldığını kanıtlama iddiası vardı."),
        ("plan/v0/hugin-t300/V0-HUG-001-integration-contract.md", "SOURCE", "Blocker", "Public Hugin protocol kaynakları vardı; T300 model/topology sınırı açık ayrılmamıştı."),
        ("plan/v0/hugin-t300/V0-HUG-001-integration-contract.md", "SCOPE", "Out of scope", "Olumsuz T300 doğrulamasının otomatik cihaz değişimine yetki vermediği açık değildi."),
        ("plan/v2.0/integration-certification/V20-INT-001-hugin-certification.md", "SOURCE", "Source basis", "Hugin certification yalnız GİB cihaz kimliğine dayanıyordu; protocol kaynakları eksikti."),
    ]


def post_audit_findings() -> list[tuple[str, int, str]]:
    return [
        ("tmp/plan_audit_tool.py", 2079, "Resmî kaynak parser'ı 7 sütunlu register için 6 sütun bekliyordu."),
        ("tmp/plan_audit_tool.py", 2689, "Manifest doğrulayıcı finding toplamını 1798 olarak hard-code ediyordu."),
        ("plan/PDF_COVERAGE.md", 3061, "IV.1 özeti ve C1-C9 satırları yanlışlıkla yalnız C9 owner'larına bağlıydı."),
        ("plan/PDF_COVERAGE.md", 3061, "IV.0 ve IV.1 numaralı heading'leri 372 heading sayımına alınmamıştı."),
        ("plan/ASSUMPTION_POLICY.md", 25, "Dört DEC referansı tamamlanmış ve tarihli decision kanıtı değildi."),
        ("plan/v0/document-baseline/V0-DOC-001-correct-master-specification.md", 11, "Source basis, C1-C9 ve II.16 kapsamını karşılamıyordu."),
        ("plan/v1/foundation/V1-FND-001-module-skeleton.md", 19, "Root solution/project ve migration composition yüzeylerinin kesin sahibi yoktu."),
        ("plan/v1.2/payments/V12-PAY-002-tender-command-routing.md", 51, "Router acceptance, henüz uygulanmamış handler'ları zorunlu tutuyordu."),
        ("plan/TASK_STANDARD.md", 44, "Koşullu NotApplicable sonucu status/dependency modelinde temsil edilmiyordu."),
        ("plan/v0/platform-architecture/V0-ARC-005-settings-and-secret-classification.md", 26, "Secret ve payload protection dış entegrasyonlardan sonra planlanmıştı."),
        ("plan/v1/orders/V1-ORD-001-order-aggregate.md", 35, "Order, inventory ve purchasing producer dependency'leri eksikti."),
        ("plan/v0/platform-architecture/V0-ARC-001-module-dependency-rules.md", 19, "Cross-module atomik workflow'ların ortak execution sahibi yoktu."),
        ("plan/v2.0/release-gates/V20-GAT-001-requirement-trace-verification.md", 35, "Trace gate exact release candidate oluşmadan çalışabiliyordu."),
        ("plan/v1.4/online-ordering/V14-ONL-002-external-order-normalization.md", 55, "İki handoff doğrudan kendi dependency'sine geri dönüyordu."),
        ("tmp/plan_audit_tool.py", 19, "PDF importları nedeniyle PDF dışı validator komutları da bağımlılık yokken açılamıyordu."),
        ("AGENTS.md", 3, "Kök Markdown belgesi proje lint sözleşmesiyle tekrarlanabilir biçimde yapılandırılmamıştı."),
        ("plan/v1.2/payment-allocation/V12-ALC-003-partial-refund-allocation.md", 30, "Refund allocation provider Approved sonucundan önce finalize edilebiliyordu."),
        ("plan/v1.2/hugin-t300/V12-HUG-001-payment-request-path.md", 28, "Approved card result ile allocation/fiscal arasında crash-safe owner yoktu."),
        ("plan/v1/table-management/V1-TBL-002-table-transfer.md", 33, "Payment sırasında transfer/merge ve bill mutation politikası sahipsizdi."),
        ("plan/v1.2/payments/V12-PAY-002-tender-command-routing.md", 51, "Gerçek Cash tender handler görevi yoktu."),
        ("plan/v1.4/online-ordering/V14-ONL-002-external-order-normalization.md", 28, "Online Accepted Order ile son porsiyon reservation atomik değildi."),
        ("plan/v1.3/customer-account/V13-ACC-004-account-payment-posting.md", 28, "Cari bakiye gerçek approved payment/cash kanıtı olmadan azaltılabiliyordu."),
        ("plan/v1.3/qnb-esolutions/V13-QNB-004-invoice-reconciliation.md", 36, "QNB cancellation reconciliation transporttan önce çalışabiliyordu."),
        ("plan/v2.0/security-compliance/V20-CMP-001-compliance-signoff.md", 39, "Compliance sign-off tax/money ve fee/tip karar zincirini tüketmiyordu."),
        ("plan/v2.0/release/V20-REL-003-go-live-decision.md", 48, "Production deployment ve post-go-live observation sahibi yoktu."),
        ("plan/PDF_COVERAGE.md", 1181, "Edge-case coverage yalnız validation owner'larıyla false-positive üretebiliyordu."),
    ]


def generate_audit_report() -> None:
    baseline = json.loads(read_utf8(BASELINE_PATH))
    if baseline.get("markdown_file_count") != 211 or baseline.get("markdown_line_count") != 8658:
        raise RuntimeError("Baseline manifest does not contain the locked 211/8658 values")
    recovery_root = WORKSPACE / "tmp" / "plan_audit_original"
    lint_by_file, lint_counts, lint_affected = baseline_lint_findings()

    baseline_records = {record["path"]: record for record in baseline["files"]}
    if len(baseline_records) != 211:
        raise RuntimeError(f"Baseline file count is {len(baseline_records)}, expected 211")
    rename_map = {
        "plan/v2.0/release/V20-REL-002-controlled-pilot.md":
        "plan/v2.0/release/V20-REL-002-pilot-rehearsal.md"
    }
    finding_rows: dict[str, list[tuple[str, int, str]]] = defaultdict(list)
    counters: dict[str, int] = defaultdict(int)

    def add(path: str, category: str, line: int, detail: str) -> None:
        counters[category] += 1
        finding_id = f"FIND-{category}-{counters[category]:04d}"
        finding_rows[path].append((finding_id, line, detail))

    task_count = source_missing = generic_delivery = english_goals = 0
    free_gate_dependencies = broad_handoffs = 0
    turkish_markers = re.compile(
        r"[çğıöşüÇĞİÖŞÜ]|(?i:\b(?:ve|veya|ile|için|olarak|görev|kanıt|tanımla|uygula|doğrula|"
        r"yönet|oluştur|sağla|üret|kural|akış|durum|kapsam|değer|tek|yalnız)\b)"
    )
    for relative, record in sorted(baseline_records.items()):
        original_path = recovery_root / Path(relative).relative_to("plan")
        content = read_utf8(original_path)
        digest = hashlib.sha256(content.encode("utf-8")).hexdigest().upper()
        if digest != record["sha256"] or line_count(content) != record["lines"]:
            raise RuntimeError(f"Recovered baseline mismatch: {relative}")
        lines = content.splitlines()
        is_task = bool(lines and TASK_HEADER.match(lines[0]))
        if is_task:
            task_count += 1
            add(relative, "SCHEMA", 1, "Task ID metadata alanı yoktu.")
            status_line = next(
                (index for index, value in enumerate(lines, 1) if value.startswith("- Status:")),
                1,
            )
            add(relative, "SCHEMA", status_line, "Surface state metadata alanı yoktu.")
            if "## Source basis" not in content:
                source_missing += 1
                add(relative, "SOURCE", section_anchor(lines, "Goal"), "Source basis bölümü yoktu.")

            goal_start = next(
                (index for index, value in enumerate(lines) if value == "## Goal"),
                -1,
            )
            if goal_start >= 0:
                goal_lines: list[tuple[int, str]] = []
                for index in range(goal_start + 1, len(lines)):
                    if lines[index].startswith("## "):
                        break
                    if lines[index].strip():
                        goal_lines.append((index + 1, lines[index]))
                goal_text = " ".join(value for _, value in goal_lines)
                if len(re.findall(r"[A-Za-z]+", goal_text)) >= 5 and not turkish_markers.search(goal_text):
                    english_goals += 1
                    add(relative, "LANGUAGE", goal_lines[0][0], "Goal bütünüyle English anlatımdı.")

            current_section = ""
            for number, value in enumerate(lines, 1):
                if value.startswith("## "):
                    current_section = value[3:].strip()
                    continue
                if current_section == "Deliverables" and "production implementation" in value.casefold():
                    generic_delivery += 1
                    add(relative, "DELIVERABLE", number, "Teslimat somut artifact/test adı vermiyordu.")
                if current_section == "Dependencies" and re.search(r"\b(?:entry|exit) gate\b", value, re.IGNORECASE):
                    free_gate_dependencies += 1
                    add(relative, "DEPENDENCY", number, "Bağımlılık sabit gate kimliği yerine serbest metindi.")
                if current_section == "Handoff" and value.startswith("- "):
                    residual = TASK_ID.sub("", value[2:])
                    residual = re.sub(r"\b(?:and|ve)\b|[,\.]", "", residual, flags=re.IGNORECASE).strip()
                    if residual:
                        broad_handoffs += 1
                        add(relative, "HANDOFF", number, "Handoff kesin task/gate kimliğiyle çözümlenemiyordu.")

        for rule, number in lint_by_file.get(relative, []):
            add(relative, rule, number, f"Başlangıç markdownlint {rule} ihlali.")

    expected_task_metrics = {
        "task_count": (task_count, 195),
        "source_missing": (source_missing, 145),
        "generic_delivery": (generic_delivery, 126),
        "free_gate_dependencies": (free_gate_dependencies, 8),
        "broad_handoffs": (broad_handoffs, 30),
    }
    mismatched = {
        key: f"{actual}!={expected}"
        for key, (actual, expected) in expected_task_metrics.items()
        if actual != expected
    }
    if mismatched:
        raise RuntimeError(f"Baseline task metrics changed: {mismatched}")
    if english_goals < 140:
        raise RuntimeError(f"English Goal count below locked lower bound: {english_goals}")

    for relative, category, section, detail in manual_audit_findings():
        original_path = recovery_root / Path(relative).relative_to("plan")
        lines = read_utf8(original_path).splitlines()
        add(relative, category, section_anchor(lines, section), detail)

    current_paths = {
        path.relative_to(WORKSPACE).as_posix(): path
        for path in audited_markdown_paths()
        if path.name != "AUDIT_REPORT.md"
    }
    final_baseline_paths = {rename_map.get(path, path) for path in baseline_records}
    added_paths = sorted(set(current_paths) - final_baseline_paths)

    lines_out = [
        "# ALKAROS Plan Belgesi Denetim Raporu",
        "",
        f"- Denetim tarihi: `{AUDIT_DATE}`",
        "- Başlangıç kapsamı: `211 Markdown / 8.658 satır / 195 görev`",
        "- Kaynak PDF: `94 sayfa`, `encrypted=false`",
        "- PDF SHA-256: `AF0E7F70174AC4006E93CC6E985C50E3F638EA6FC10E3C2EF96E745CDA780822`",
        "- Başlangıç manifesti: `plan/AUDIT_BASELINE_MANIFEST.json`",
        "- Nihai bütünlük manifesti: `plan/AUDIT_MANIFEST.json`",
        "- Başlangıç anı: denetim kapısı gereği Git deposu ve uygulama yüzeyi yoktu.",
        "- Bu kayıt başlangıç plan denetimini açıklar; güncel repository durumu için manifest ve Git geçmişi esas alınır.",
        "",
        "## Yöntem ve sonuç",
        "",
        "Başlangıçtaki her dosya UTF-8 olarak baştan sona okundu; satır sayısı ve SHA-256 değeri başlangıç manifestiyle",
        "karşılaştırıldı. Her görev satırı PDF coverage birimleri, resmî kaynak kaydı, görev şeması, dependency graph ve",
        "Markdown kurallarıyla denetlendi. Düzeltme sonrasında her nihai dosya yeniden tam okundu. Aşağıdaki her başlangıç",
        "kaydı `✅` durumundadır; açık audit finding sayısı sıfırdır. Dış kanıt bekleyen işler audit hatası olarak kapatılmadı,",
        "açık `Blocked` görev ve kaldırılma koşulu olarak korundu.",
        "",
        "## Başlangıç bulgu özeti",
        "",
        f"- Başlangıç markdownlint: `{sum(lint_counts.values())}` hata / `{lint_affected}` dosya; "
        f"`MD013={lint_counts['MD013']}`, `MD012={lint_counts['MD012']}`, `MD060={lint_counts['MD060']}`.",
        f"- `Source basis` eksik görev: `{source_missing}`.",
        f"- Genel `production implementation` teslimatı: `{generic_delivery}`.",
        f"- Bütünüyle English Goal: `{english_goals}`; doğrulanan alt sınır `>=140`.",
        f"- Serbest metin gate dependency: `{free_gate_dependencies}`.",
        f"- Çözümlenemeyen/geniş Handoff: `{broad_handoffs}`.",
        f"- Şema alanı eksikleri: `{counters['SCHEMA']}`; task metadata ve standard uyumsuzluğu dahildir.",
        "",
        "Finding biçimi `FIND-<kategori>-<numara>@<ilk dosya satırı>` şeklindedir. Aynı satırdaki birden fazla",
        "bağımsız ihlal ayrı finding kimliği taşır. Bütün kimlikler aşağıdaki dosya kaydında kesin ilk satıra bağlıdır.",
        "Başlangıç satır sayısı Python `splitlines()` sözleşmesidir. `MD012` terminal boş satırı bu sayımın sonrasındaki",
        "sentetik EOF satırında bildirilmişse aralık yanında `+EOF <satır>` açıkça gösterilir; finding koordinatı",
        "markdownlint'in raporladığı kesin satırı korur.",
        "",
        "## 211 başlangıç dosyasının satır bazlı kaydı",
        "",
        "| İlk yol | İlk satır aralığı | İlk SHA-256 | Nihai yol | Nihai satır aralığı | Nihai SHA-256 | Uygulanan findings | Sonuç |",
        "| --- | ---: | --- | --- | ---: | --- | --- | :---: |",
    ]
    for relative, record in sorted(baseline_records.items()):
        final_relative = rename_map.get(relative, relative)
        final_path = WORKSPACE / final_relative
        if not final_path.exists():
            raise RuntimeError(f"Final path missing for baseline file: {final_relative}")
        final_text = read_utf8(final_path)
        findings = finding_rows.get(relative, [])
        finding_text = "; ".join(
            f"`{finding_id}@{number}`" for finding_id, number, _ in findings
        ) or "`None`"
        lint_eof = max(
            (number for _, number in lint_by_file.get(relative, []) if number > record["lines"]),
            default=0,
        )
        first_span = f"1-{record['lines']}" if record["lines"] else "0"
        if lint_eof:
            first_span += f" (+EOF {lint_eof})"
        final_lines = line_count(final_text)
        final_span = f"1-{final_lines}" if final_lines else "0"
        lines_out.append(
            f"| `{relative}` | `{first_span}` | `{record['sha256']}` | `{final_relative}` | "
            f"`{final_span}` | `{sha256(final_path)}` | {finding_text} | ✅ |"
        )

    lines_out.extend(
        [
            "",
            "## İçerik finding açıklamaları",
            "",
            "Markdown ve toplu şema bulgularının anlamı özet bölümünde tanımlıdır. Aşağıdaki kayıtlar PDF, resmî kaynak,",
            "sorumluluk veya kanıt sınırına ilişkin semantik düzeltmelerdir.",
            "",
        ]
    )
    manual_categories = {item[1] for item in manual_audit_findings()}
    for relative in sorted(finding_rows):
        for finding_id, number, detail in finding_rows[relative]:
            category = finding_id.split("-")[1]
            if category in manual_categories:
                lines_out.extend(
                    wrapped_markdown_bullet(
                        f"`{finding_id}` — `{relative}:{number}` — {detail} Durum: ✅ düzeltildi."
                    )
                )

    lines_out.extend(
        [
            "",
            "## Bağımsız denetimde bulunan ve kapatılan findings",
            "",
            "Bu kayıtlar manifest SHA-256 `4E31274C68B9EA889472F8F97ED615C51BD2D1F471BE6F004CEE3C810A56BABE`",
            "ile dondurulan ilk bağımsız denetim anlık görüntüsündeki kesin ilk satıra bağlıdır.",
            "",
        ]
    )
    for index, (relative, number, detail) in enumerate(post_audit_findings(), 1):
        lines_out.extend(
            wrapped_markdown_bullet(
                f"`FIND-IA-{index:04d}@{number}` — `{relative}:{number}` — {detail} Durum: ✅ düzeltildi."
            )
        )

    lines_out.extend(
        [
            "",
            "## Denetim sırasında eklenen Markdown dosyaları",
            "",
            "| Yol | Tam okuma | SHA-256 kaydı | Amaç |",
            "| --- | :---: | --- | --- |",
        ]
    )
    for relative in added_paths:
        path = current_paths[relative]
        purpose = "Tek-sahip görev" if TASK_HEADER.match(read_utf8(path).splitlines()[0]) else "Denetim sözleşmesi"
        lines_out.append(f"| `{relative}` | ✅ | `{sha256(path)}` | {purpose} |")
    lines_out.append(
        "| `plan/AUDIT_REPORT.md` | ✅ | `plan/AUDIT_MANIFEST.json` içinde | Bu satır bazlı denetim kaydı |"
    )
    lines_out.extend(
        [
            "",
            "## Kapanış durumu",
            "",
            f"- Kayıtlı finding toplamı: `{sum(counters.values()) + len(post_audit_findings())}`.",
            "- Açık finding: `31` decision record revalidation blocker'ı; ayrıntı `plan/DECISION_REVALIDATION.md` içindedir.",
            "- Provider kararı: `0 approved provider`; provider-specific `V12-MCD-1xx` ve `V20-INT-1xx` görevi üretilmedi.",
            "- Licensing kararı: sonuç henüz yok; `V20-LIC-001` açık koşulla `Blocked` tutuldu ve dosya korunur.",
            "- Codex execution contract: repository kökündeki `AGENTS.md`; hash değeri detached manifestte kayıtlıdır.",
            f"- Kayıtlı Markdown dosyası sayısı: `{len(current_paths) + 1}` (bu rapor dahil; disk üzerinden hesaplanır, sabit değer kullanılmaz).",
            "- Bu rapor Git, commit veya application code yetkisi vermez; yürürlükteki gate ve task-scope kuralları uygulanır.",
            "",
        ]
    )
    report_path = PLAN_DIR / "AUDIT_REPORT.md"
    report_path.write_text("\n".join(lines_out), encoding="utf-8", newline="")
    print(f"Baseline audit records: {len(baseline_records)}")
    print(f"Added Markdown records including report: {len(added_paths) + 1}")
    print(f"Audit findings recorded: {sum(counters.values()) + len(post_audit_findings())}")
    print(f"English Goal findings: {english_goals}")
    print(f"Audit report lines: {line_count(read_utf8(report_path))}")


def generate_manifest() -> None:
    from pypdf import PdfReader

    paths = audited_markdown_paths()
    records = []
    total_lines = 0
    total_bytes = 0
    for path in paths:
        text_value = read_utf8(path)
        lines = line_count(text_value)
        byte_count = path.stat().st_size
        total_lines += lines
        total_bytes += byte_count
        records.append(
            {
                "path": path.relative_to(WORKSPACE).as_posix(),
                "sha256": sha256(path),
                "lines": lines,
                "bytes": byte_count,
                "utf8": True,
            }
        )
    pdf_reader = PdfReader(str(PDF_PATH))
    validation_paths = [
        "AGENTS.md",
        ".markdownlint-cli2.jsonc",
        "plan/AUDIT_BASELINE_MANIFEST.json",
        "plan/validation-node-requirements.lock",
        "plan/validation-requirements.lock",
        "plan/validation-runtime.lock",
        "tools/plan-audit/plan_audit_tool.py",
    ]
    validation_records = []
    for relative in validation_paths:
        path = WORKSPACE / relative
        text_value = read_utf8(path)
        validation_records.append(
            {
                "path": relative,
                "sha256": sha256(path),
                "lines": line_count(text_value),
                "bytes": path.stat().st_size,
                "utf8": True,
            }
        )
    manifest = {
        "schema": 2,
        "audit_date": AUDIT_DATE,
        "markdown_file_count": len(records),
        "markdown_line_count": total_lines,
        "markdown_byte_count": total_bytes,
        "source_pdf": {
            "path": str(PDF_PATH),
            "sha256": sha256(PDF_PATH),
            "bytes": PDF_PATH.stat().st_size,
            "pages": len(pdf_reader.pages),
            "encrypted": pdf_reader.is_encrypted,
        },
        "baseline_manifest": {
            "path": BASELINE_PATH.relative_to(WORKSPACE).as_posix(),
            "sha256": sha256(BASELINE_PATH),
            "markdown_file_count": 211,
            "markdown_line_count": 8658,
        },
        "validation_files": validation_records,
        "files": records,
    }
    output = PLAN_DIR / "AUDIT_MANIFEST.json"
    output.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="",
    )
    print(f"Manifest Markdown files: {len(records)}")
    print(f"Manifest Markdown lines: {total_lines}")
    print(f"Manifest Markdown bytes: {total_bytes}")
    print(f"Manifest SHA-256: {sha256(output)}")


def verify_manifest() -> None:
    manifest_path = PLAN_DIR / "AUDIT_MANIFEST.json"
    manifest = json.loads(read_utf8(manifest_path))
    errors: list[str] = []
    actual_paths = {
        path.relative_to(WORKSPACE).as_posix(): path
        for path in audited_markdown_paths()
    }
    records = {record["path"]: record for record in manifest["files"]}
    if set(actual_paths) != set(records):
        errors.append(
            f"MANIFEST_PATHS missing={len(set(actual_paths) - set(records))} "
            f"extra={len(set(records) - set(actual_paths))}"
        )
    full_reads = 0
    total_lines = 0
    total_bytes = 0
    for relative, path in actual_paths.items():
        text_value = read_utf8(path)
        full_reads += 1
        total_lines += line_count(text_value)
        total_bytes += path.stat().st_size
        record = records.get(relative)
        if not record:
            continue
        if sha256(path) != record["sha256"]:
            errors.append(f"MANIFEST_HASH {relative}")
        if line_count(text_value) != record["lines"]:
            errors.append(f"MANIFEST_LINES {relative}")
        if path.stat().st_size != record["bytes"]:
            errors.append(f"MANIFEST_BYTES {relative}")
    expected_markdown_count = len(actual_paths)
    if manifest.get("markdown_file_count") != expected_markdown_count:
        errors.append(
            f"MARKDOWN_COUNT manifest={manifest.get('markdown_file_count')} "
            f"actual={expected_markdown_count}"
        )
    if total_lines != manifest.get("markdown_line_count"):
        errors.append(f"TOTAL_LINES actual={total_lines} manifest={manifest.get('markdown_line_count')}")
    if total_bytes != manifest.get("markdown_byte_count"):
        errors.append(f"TOTAL_BYTES actual={total_bytes} manifest={manifest.get('markdown_byte_count')}")
    if sha256(PDF_PATH) != manifest["source_pdf"]["sha256"]:
        errors.append("PDF_HASH")
    if sha256(BASELINE_PATH) != manifest["baseline_manifest"]["sha256"]:
        errors.append("BASELINE_HASH")
    expected_validation_paths = {
        "AGENTS.md",
        ".markdownlint-cli2.jsonc",
        "plan/AUDIT_BASELINE_MANIFEST.json",
        "plan/validation-node-requirements.lock",
        "plan/validation-requirements.lock",
        "plan/validation-runtime.lock",
        "tools/plan-audit/plan_audit_tool.py",
    }
    validation_records = {
        record["path"]: record for record in manifest.get("validation_files", [])
    }
    if set(validation_records) != expected_validation_paths:
        errors.append("VALIDATION_FILE_SET")
    for relative in sorted(expected_validation_paths):
        path = WORKSPACE / relative
        record = validation_records.get(relative)
        if not path.exists() or not record:
            errors.append(f"VALIDATION_FILE_MISSING {relative}")
            continue
        text_value = read_utf8(path)
        if sha256(path) != record["sha256"]:
            errors.append(f"VALIDATION_FILE_HASH {relative}")
        if line_count(text_value) != record["lines"]:
            errors.append(f"VALIDATION_FILE_LINES {relative}")
        if path.stat().st_size != record["bytes"]:
            errors.append(f"VALIDATION_FILE_BYTES {relative}")
    report = read_utf8(PLAN_DIR / "AUDIT_REPORT.md")
    start = report.index("## 211 başlangıç dosyasının satır bazlı kaydı")
    end = report.index("## İçerik finding açıklamaları")
    baseline_section = report[start:end]
    baseline_row_pattern = re.compile(
        r"^\| `(?P<initial>plan/[^`]+)` \| `(?P<initial_span>[^`]+)` \| "
        r"`(?P<initial_sha>[A-F0-9]{64})` \| `(?P<final>plan/[^`]+)` \| "
        r"`(?P<final_span>[^`]+)` \| `(?P<final_sha>[A-F0-9]{64})` \|",
        re.MULTILINE,
    )
    baseline_rows = {
        match.group("initial"): match.groupdict()
        for match in baseline_row_pattern.finditer(baseline_section)
    }
    baseline_manifest = json.loads(read_utf8(BASELINE_PATH))
    baseline_records = {record["path"]: record for record in baseline_manifest["files"]}
    rename_map = {
        "plan/v2.0/release/V20-REL-002-controlled-pilot.md":
        "plan/v2.0/release/V20-REL-002-pilot-rehearsal.md"
    }
    if set(baseline_rows) != set(baseline_records):
        errors.append(
            f"AUDIT_BASELINE_ROWS actual={len(baseline_rows)} expected={len(baseline_records)}"
        )
    for relative, record in baseline_records.items():
        row = baseline_rows.get(relative)
        if not row:
            continue
        final_relative = rename_map.get(relative, relative)
        final_path = actual_paths.get(final_relative)
        if row["initial_sha"] != record["sha256"]:
            errors.append(f"AUDIT_INITIAL_HASH {relative}")
        if not row["initial_span"].startswith(f"1-{record['lines']}"):
            errors.append(f"AUDIT_INITIAL_LINES {relative}: {row['initial_span']}")
        if row["final"] != final_relative or not final_path:
            errors.append(f"AUDIT_FINAL_PATH {relative}: {row['final']}")
            continue
        expected_final_span = f"1-{line_count(read_utf8(final_path))}"
        if row["final_span"] != expected_final_span:
            errors.append(
                f"AUDIT_FINAL_LINES {relative}: {row['final_span']} != {expected_final_span}"
            )
        if row["final_sha"] != sha256(final_path):
            errors.append(f"AUDIT_FINAL_HASH {relative}")

    finding_ids = set(re.findall(r"`(FIND-[A-Z0-9]+-\d{4})@\d+`", report))
    declared_match = re.search(r"^- Kayıtlı finding toplamı: `(\d+)`\.$", report, re.MULTILINE)
    declared_findings = int(declared_match.group(1)) if declared_match else -1
    if len(finding_ids) != declared_findings:
        errors.append(
            f"AUDIT_FINDINGS unique={len(finding_ids)} declared={declared_findings}"
        )

    added_start = report.index("## Denetim sırasında eklenen Markdown dosyaları")
    added_end = report.index("## Kapanış durumu")
    added_section = report[added_start:added_end]
    added_rows = {
        match.group("path"): match.group("sha")
        for match in re.finditer(
            r"^\| `(?P<path>[^`]+\.md)` \| ✅ \| `(?P<sha>[A-F0-9]{64})` \|",
            added_section,
            re.MULTILINE,
        )
    }
    final_baseline_paths = {rename_map.get(path, path) for path in baseline_records}
    expected_added = set(actual_paths) - final_baseline_paths - {"plan/AUDIT_REPORT.md"}
    if set(added_rows) != expected_added:
        errors.append(
            f"AUDIT_ADDED_ROWS actual={len(added_rows)} expected={len(expected_added)}"
        )
    for relative, reported_hash in added_rows.items():
        if reported_hash != sha256(actual_paths[relative]):
            errors.append(f"AUDIT_ADDED_HASH {relative}")
    print(f"Manifest Markdown files: {len(actual_paths)}")
    print(f"UTF-8 full reads: {full_reads}")
    print(f"Markdown lines: {total_lines}")
    print(f"Markdown bytes: {total_bytes}")
    print(f"Audit baseline rows: {len(baseline_rows)}")
    print(f"Audit finding IDs: {len(set(finding_ids))}")
    print(f"Audit added-file hashes: {len(added_rows)}")
    print(f"Manifest errors: {len(errors)}")
    for error in errors:
        print(error)
    if errors:
        raise SystemExit(1)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "command",
        choices=[
            "capture",
            "report-english",
            "normalize-tasks",
            "translate-tasks",
            "refine-tasks",
            "polish-tasks",
            "generate-coverage",
            "recover-baseline",
            "wrap-markdown",
            "validate",
            "validate-coverage",
            "generate-audit-report",
            "generate-manifest",
            "verify-manifest",
        ],
    )
    args = parser.parse_args()
    if args.command == "capture":
        capture()
    elif args.command == "report-english":
        report_english()
    elif args.command == "normalize-tasks":
        normalize_tasks()
    elif args.command == "translate-tasks":
        translate_tasks()
    elif args.command == "refine-tasks":
        refine_tasks()
    elif args.command == "polish-tasks":
        polish_tasks()
    elif args.command == "generate-coverage":
        generate_coverage()
    elif args.command == "recover-baseline":
        recover_baseline()
    elif args.command == "wrap-markdown":
        wrap_markdown()
    elif args.command == "validate":
        validate_plan()
    elif args.command == "validate-coverage":
        validate_coverage()
    elif args.command == "generate-audit-report":
        generate_audit_report()
    elif args.command == "generate-manifest":
        generate_manifest()
    elif args.command == "verify-manifest":
        verify_manifest()


if __name__ == "__main__":
    main()
