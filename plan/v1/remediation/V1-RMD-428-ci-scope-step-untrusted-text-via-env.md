# V1-RMD-428 - CI kapsam adımında PR ve commit metinlerinin betiğe gömülmeden ortam değişkeniyle geçirilmesi

- Task ID: V1-RMD-428
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

PR #11'in `pull_request` koşusunda "Task scope enforcement" işi, görev kimliklerini çözen adımda ayrıştırma hatasıyla
düştü: adım PR başlığını ve açıklamasını `"${{ github.event.pull_request.body }}"` biçiminde PowerShell betiğinin
kaynağına doğrudan gömüyordu; açıklamadaki bir çift tırnak (`"kendi hesabı"`) dizeyi kapatıp betiği bozdu. Aynı kalıp
`push` olayında commit mesajı için de var; çift tırnaklı bir commit mesajı master'daki denetimi de kırar. Bu aynı
zamanda bir betik enjeksiyonu açığıdır: PR başlığı/açıklaması yazabilen biri CI'da PowerShell kodu çalıştırabilir
(GitHub'ın "untrusted input" uyarısı).

Bu görev: kapsam işinin iki adımındaki kullanıcı kaynaklı bütün değerler (PR başlığı ve açıklaması, commit mesajı,
`workflow_dispatch` girdileri, taban ref'leri) adımın `env:` bölümüne alınır ve betikte `$env:...` olarak okunur;
betik kaynağına hiçbir metin gömülmez. Davranış (hangi görev kimliklerinin ve tabanın seçildiği) değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-428-ci-scope-step-untrusted-text-via-env.md`
- `evidence/V1-RMD-428/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): .github/workflows/task-scope.yml (CI iş akışı sahipliğinde) — yalnız
  `enforce` işinin "Resolve diff base" ve "Resolve Task ID(s)" adımları

## In scope

- İki adımda `${{ ... }}` ifadelerinin `env:` üzerinden geçirilmesi.

## Out of scope

- İş akışının diğer işleri ve adımları.

## Dependencies

- V1-RMD-427

## Acceptance evidence

- Kapanışta doldurulacak.

## Handoff

- None
