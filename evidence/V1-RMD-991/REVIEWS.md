# V1-RMD-991 Reviews

## qa-operasyon-gozlemlenebilirlik
Applied: command now documents --base <start commit> (a default of HEAD gives a false green after the commit), --base without a value fails, CI wording corrected (CI runs the same three steps through pnpm build; the script's test runs in the Node contract step).
Open: only text and the CI build enforce the rule; the local closing hook is outside the repo and does not call the script. A client with a package.json but without lint/typecheck scripts would fail the gate (only PosTerminal exists today). The gate uses npm while the project locks with pnpm.

## qa-test-kalitesi
Applied: the lint test runs the production lint script text from package.json against the fixture (cap and rule level now locked by a test), expects exit 1 and the rule name, requires installed dependencies, real Windows-path test.
Open: main() and the step order are not unit tested (covered by green-gate.log / red-gate.log).
