# V1-RMD-720 - Orders test boşlukları ve TableDraft test düzeneği

- Task ID: V1-RMD-720
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B003 ve B032 kapanır. TableDraft test projesi migration 205'i düzeneğe bağlar ve 10 kırmızı test yeşile döner. Mutasyon denetiminde hayatta kalan sekiz mutantı öldüren testler eklenir: yeniden deneme yolunda çift fiş, iptal ve ikram kalem için stok tüketilmemesi, ikram sayımı, yarım değer yuvarlama, kurs ve indirim sınırı; ayrıca gönderilmiş kalem iptalinde onay ve yeniden deneme, audit hatasında stok iadesi atomikliği (Sent kalemle) ve mutfak/hesap adımı hata yolu.

## Owned surface

- `plan/v1/remediation/V1-RMD-720-orders-test-gaps-and-tabledraft-migration-link.md`
- `evidence/V1-RMD-720/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/** - yalnız test projesi bağlantısı ve yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/** - yalnız yeni test
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Test projesi migration bağlantısı ve sekiz hayatta kalan mutant için testler. Üretim kodu değişmez.

## Out of scope

- Üretim kodu düzeltmeleri (V1-RMD-721, 722, 723).

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-720/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Mutasyon kanıtı: sekiz mutantın hepsi bu görevin testleriyle KILLED olur.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
