# V1-RMD-974 Result

Status: Done.

- A manager can give each terminal a name (migration 226, PUT .../security/terminals/{id}/name): trimmed, 1-40 characters, no control characters, unique ignoring case (409 NAME_TAKEN), blank clears it; each change is audited with the previous name.
- The terminals list returns the name; GET /auth/session returns terminalName. The Terminaller screen has a name field and "Adı kaydet" per terminal; the top bar identity and the release button label use the name, with the short id as the fallback.
- Tests: SecurityAdministration 30/30, session name 1/1, migration/manifest 18/18, ApiConventions 2/2, PosTerminal routes+shell 272/272 and security panel 33/33, tsc clean; 9 mutants killed.
- Not captured: a separate red run (tests and server code were written in one pass); the mutants prove the tests catch each behaviour.
- Known and accepted: the open top bar shows a renamed terminal's name after the next sign-in; the login screen (before a session exists) still shows the short id; lower() follows the database collation, so Turkish dotted/dotless I variants are treated as different letters.
