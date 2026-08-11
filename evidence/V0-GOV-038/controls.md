# V0-GOV-038 Immutable History Attestation

- Candidate: `0c8cd75fbebeacfdf455f24de9b13c5ee7434da6`
- Root: `8d466ba540f74025ac17e3f29d367333fd16d4c1`
- Root..candidate commit count: `157` (measured 2026-08-11)
- Commit-sequence SHA-256: `4ABB78702C89E7F19BAA9D409CD6B56F0C9D530AF6DA904939435EA728867DBE`
- Frozen CORR:C52 input SHA-256: `35CFE716B72FC07C2B660EE2E04707A10D038161C5B45F9F7FB28A753A042DF8`
- The first 145 rows are hash-checked against the frozen C52 ledger and each row's Git identity, parent and changed-path list is rechecked against the candidate.
- The final 12 rows are measured from the candidate Git objects. No history object is changed by this task.

## Separate verdicts

- Commit-time scope failures: `45` — `789a55b6c77369226153a5f7528a6e6a7dadef0c, 4ccd4cfd1c9fac880984a683d0dee5c04d187782, 2e635c4ce9057b7ae6a438843ece0077bed88b4f, ef995511ad72925c4c5ca4ebe46c27b3949e8993, 9105219bb35f01c5d08053a2a55ea72a36aca833, e578186f4895094218d6926396a616c660bbf5de, f4f7c061c84c429e5ce76f58ac1c3a4f0497e1ad, 142557583233ed6d29505a362ca9e3bcbd1fce0a, 952240193e4a03bb69ba32e5a0fe3adbcf35949e, 585d1fb04174744b409f855a11e27097900fe177, 2d608bbb962c488c553c935a54e4fcb3e5cbfd78, eeb74d4e92a7b5d68d10cb0b89203047ce9e28d4, 0af41d8aa5ff294f521055deeffd2e98df7d8715, c3bc620237e04aa6d1cb7c5f5dbda5177e9880ba, 529667b1f2c7e5a9a30d2a3adc72e66d8a3afee7, 0a86027deb65d1c1449b262cb7a18df3d0c94c13, 950efca1c6eb783161d6ea003e2633514512d64f, 2d0d7e7754ea502f33c1f7111bdb46b88d93d6ad, 86ed846da9a951b0a268b76955652a29d91268be, d0cbcbea340acd619d3686358faa1d9fea69da0f, 6820ae73bd73565df29f4802948735915e2c9f3e, b65d2bb3dd371a273b968d9f50f1942eea1200b5, 564bd049e75f60ea8deedf0d5993417935f6793e, 0980df22c54979dd2d5ce348405a6f2437b898ed, 10bc6b1d7fe4fdb870f3c5db05e97fe6f26a4726, f2e03984b014cc06ca4f304f4a58ea2fe0215f62, 5106b1849daa43aa5e3c15fb32cfdcb19610b1d8, ef1091cc78db2c6858f2d2e383e5e2caf1917d68, 371acb36ac62bfc4bc0e17f1545f6392b003a8e4, 00cc6663ac88f6a0518302726c010ce5fd7f2c14, 9528f783e26a1248d490c28b1989556fec5fcbf7, ded51aa324b6bc8e5ca23fa252469dbeffa6b85a, ca7cde9b87ebc136ffaf8b33bad65452e706843e, 55c81673d69eeadf55f3d137cb5b667309001554, 439361e402536d4ba7974c61db11287f0ab61506, 983f25d077ec2707af76559260b9fcea765b069b, 00b78a8ff2a65574a9117e6c371697dfb5f3e853, 0cdc35d16d16e4b61160694e17b19b17e0701d0e, 813e5eecceb11327a11fe7f2e5b45b6ba1bb7ed8, 38571267c5e37f46d3fafbb11022d617d2ad9b46, 8f8e6b1bb0afb518ce2be4cf8bb83bf4376fa959, 8fa12b3ec8837f1c1fa0e7b9da7793d1f5cd9188, a4e48ad5aa53d851c561f5ddf0ebcb37a3bc17b7, 8bf1100d8c835f9b46f885d36de8840e480432a5, 2277d4e60e3dbabb93ffad2586062b96c3d0d415`
- C52 current-contract snapshot failures: `53`. These are kept separate from the retrospective verdict in every C52 row.
- Extension rows carry independently derived historical and candidate-current verdicts where a canonical Task trailer exists; otherwise both are `UNATTRIBUTED`.

## Footer control

- C52 missing-footer set (`13`): `81320187fb24dbabb8c2bbe021b5cab6adbc9605, 37a44b5c68c852943e88801dd93fa7e3bf5913f4, a55854667b1846fffe82aaa9992a45c35fa7aed9, f912e409e7306f4494955462fb1199077db7f7e1, 7526fc8d3c3016f045a3f503f2fe1596394a4f1e, 4e5330211641a1f127ac3625d24ab02cc24fc95b, fdab1da98edc4c81e928e3de0dcfd2f6b6beb678, 9e8471086e28ba9706ed8044041fa1d7459c600d, 974d9fc1649f74f185114bd334c9f949a8aa8893, ef92770e4e4f2e36ed276082d715132d8d64a748, 750110821347b57632d99c23de48681284996812, d2b066334d79028c3d31d4d3922600fd8c175af3, 825882aaaa2a9483694120cab4f65017da93ffc1`
- The other 11 C52 `MISSING` rows are the C45 immutable unattributed exceptions; they are explicitly tagged separately and do not inflate the C52 GOV-003 13-commit set.
- Immutable exceptions outside that C52 set:
  - `2afa0c3445279be8a5fb3ba80fa2c3d0d22484c6`: literal `\n` bytes keep `Task:` and `Gate:` out of a trailer block.
  - `0f2efe6616a90007d326c0d1870a436f0ae2577e`: blank line separates `Task:` from the trailing `Gate:` line, so it is not a contiguous canonical trailer block.
- Disposition: attest these objects; do not rebase, amend, force-push or otherwise rewrite them.

## Generated files

- `history-ledger.json`: full structured rows, including changed paths, C52 historical/current verdicts and status transitions.
- `history-ledger.csv`: one compact row per commit with the same changed-path and verdict data.
- `preservation.json`: before/after candidate boundary fingerprint.
