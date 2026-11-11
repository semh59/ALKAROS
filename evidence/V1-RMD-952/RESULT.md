# V1-RMD-952 Result

Status: Done.

- Onayla on a pending approval card now opens a question ("Emin misiniz? <permission> <amount>") with "Evet, onayla" and "Vazgeç"; the request is sent only on the second click. Reddet stays one step.
- Only one question is open at a time; opening another card or denying closes it.
- Tests: PosTerminal authorization-decisions 14/14; 7 mutants killed (mutations.txt).
- Open low note: no focus move or Escape on the question, same as the other two-step prompts in the app.
