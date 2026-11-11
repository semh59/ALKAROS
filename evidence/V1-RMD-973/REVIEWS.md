# V1-RMD-973 Reviews

- qa-backend: ONAY (race handled by bill row lock; lateReplay branch had no test, removed; early replay check precedes the lock, so a concurrent same-key duplicate can get 409, accepted).
- qa-frontend: koşullu ONAY; findings fixed (key kept on 5xx, new key when the reason changes) with two tests and mutants M4/M5 killed.
