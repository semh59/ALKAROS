# V1-RMD-954 Result

Status: Done.

- A discount or comp waiting for a manager is now checked by the page itself every 5 seconds (same request, same key); when the manager decides, the applied or refused message appears without pressing the button. The button stays; the notice says so.
- The check is silent while still waiting or on server failure (no redraw, no focus loss), pauses on a hidden tab and during a payment, and stops when the form is edited or the request is decided.
- Tests: split-payment suite 242/242 (19 files); mutants in mutations.txt.
