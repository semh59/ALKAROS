# V1-RMD-081 - Recurring consistency audit tooling

- Task ID: V1-RMD-081
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Farklı araç ve oturumların gelecekte yeni İngilizce string sızıntısı veya Türkçe kod kimliği üretmesini önlemek için, tek seferlik değil periyodik çalıştırılabilir bir tutarlılık denetim aracı ve yordamı kurmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-081-recurring-consistency-audit-tooling.md`
- `tools/consistency-audit/**`
- `docs/CONSISTENCY_AUDIT.md`

## In scope

- `tools/consistency-audit/consistency_audit.py`: kullanıcıya görünen İngilizce string sızıntısını (`docs/UI_STYLE_GUIDE.md` terim sözlüğüne göre) ve şema/kod kimliklerinde Türkçe karakter kullanımını sıfır dış bağımlılıkla tarayan, ihlalde sıfırdan farklı çıkış kodu veren betik.
- `docs/CONSISTENCY_AUDIT.md`: aracın ne taradığı, nasıl çalıştırıldığı, hangi dosya kümesini kapsadığı ve her kurtarma dalgası ile sürüm kapısından önce çalıştırılması gerektiği.

## Out of scope

- Betiği CI iş akışına bağlamak; bu ayrı bir foundation görevine aittir.
- Mevcut ihlalleri düzeltmek; bu `V1-RMD-080` kapsamındadır.

## Dependencies

- V1-RMD-080

## Deliverables

- Çalıştırılabilir `consistency_audit.py` betiği ve `docs/CONSISTENCY_AUDIT.md` yordam dokümanı.

## Acceptance evidence

- `python tools/consistency-audit/consistency_audit.py` mevcut kod tabanında sıfır çıkış kodu verir.
- Semih; betiğe bilerek Türkçe bir kod kimliği veya çevrilmemiş bir arayüz metni eklendiğinde sıfırdan farklı çıkış kodu ve ihlal satırının raporlandığını doğrular.

## Handoff

- V1-GOV-043
