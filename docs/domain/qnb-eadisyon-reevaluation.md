# QNB e-Adisyon decision: re-evaluation brief

> **Task:** V1-RMD-889
> **Concerns:** `V13-FSC-005` (status `NotApplicable`, 2026-09-18)
> **Prepared:** 2026-11-07, at the product owner's request
> **Status:** brief for a decision. No decision is taken here and no status, code or plan dependency changes.

## 1. Where the decision stands

`V13-FSC-005` was set to `NotApplicable` on 2026-09-18 (Semih, preliminary technical choice, not the accountant sign-off that `V0-CMP-001` still owns) for three reasons:

1. QNB's public documentation does not contain the word "adisyon", so there is no verifiable contract surface.
2. `V13-FSC-004` already treats the Token/Beko device as a real YN OKC / e-Adisyon device, so a separate QNB integration looks unnecessary.
3. The product owner preferred the Token/Beko branch over the QNB branch.

The product owner asked on 2026-11-07 for the decision to be re-evaluated, because the 2026-10-17 external-dependency research (recorded in `V1-RMD-584` notes of `V0-QNB-001`, `V0-CMP-001` and in the note inside `V13-FSC-005`) contradicts parts of the reasoning.

## 2. What is known

Recorded in the 2026-10-17 research notes (`V1-RMD-584`, `V1-RMD-881`); the primary GIB pages and guide were not re-read in this task and should be re-checked before anyone relies on them:

- The GIB list of private e-Adisyon integrators (`https://ebelge.gib.gov.tr/eadisyonbelgesiozelentegratorlerlistesi.html`, page dated 25.09.2026) includes QNB eSolutions. Token/Beko is not on it.
- The GIB e-Arsiv Technical Guide v1.19 has an e-Adisyon report section (including `adisyonIptal`). Reason 1 ("no public documentation") therefore holds only for QNB's own product API, not for the e-Adisyon document model itself.
- QNB's WSDL/XSD files are public, but they cover the e-Fatura / e-Arsiv services already used by ALKAROS; no QNB e-Adisyon service definition was found.

Carried over from the `V13-FSC-005` note and not re-read in this task: e-Adisyon is not mandatory; it becomes mandatory only on a written GIB notice with at least three months' lead (VUK 509, IV.12.2 and IV.12.4).

Not verified (stated as unknown, not as a finding):

- Whether QNB offers an e-Adisyon API to its customers and under what contract.
- Whether the Token/Beko cloud issues e-Adisyon as a separate e-document. The device itself is not an e-Adisyon device (see the next section); Token's absence from the GIB integrator list is consistent with that.
- Whether a venue that issues a YN OKC receipt gains or loses anything legally by also sending e-Adisyon.

## 3. Status of the three reasons

| Reason | Status after research |
|---|---|
| 1. No public QNB documentation | Partly invalid: the e-Adisyon document model is public (GIB guide); QNB's own API is still undocumented. |
| 2. Token/Beko already covers it | Contradicted by the recorded `V13-FSC-004` research note (GIB documents read on 2026-10-16/17): e-Adisyon is an e-document issued through a private integrator or direct integration, not a device, and a Token/Beko device cannot be defined as an e-Adisyon device. The device prints the YN OKC fiscal receipt; it does not replace e-Adisyon. |
| 3. Product owner preference for Token/Beko | Still a valid preference; it does not depend on 1 and 2. |

## 4. Open questions and who answers them

| Question | Owner |
|---|---|
| Is e-Adisyon needed or advisable for this venue next to the YN OKC receipt? (`V0-CMP-001` is Done, but its matrix correction and fourth question are still open.) | Certified financial advisor, through `V0-CMP-001` |
| Does the Token/Beko device satisfy the venue's fiscal duty without e-Adisyon? | Certified financial advisor, Token |
| Does QNB sell and document an e-Adisyon API, with test tenant and `erpKodu`? | Product owner, QNB account contact |
| Is the NotApplicable decision kept, reopened for a later version, or reopened now? | Product owner |

## 5. Effect of each outcome

- Decision kept: no task changes. The reasoning in the `V13-FSC-005` approval text should be corrected for reasons 1 and 2.
- Decision reopened: `V13-FSC-005` returns to a planned or blocked state, blocked on an exact QNB contract and sandbox transcript (`V0-QNB-001`) and on the accountant decision (`V0-CMP-001`). No adapter or stub may be written before that. Its handoffs (listed in the task file) would need re-checking.

## 6. Suggested order (not a decision)

1. Get the accountant's answer to the first question in section 4 through `V0-CMP-001`.
2. Only if e-Adisyon is wanted, ask QNB for the e-Adisyon service definition and a test tenant.
3. Decide on `V13-FSC-005` with both answers in hand.
