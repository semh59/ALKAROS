# V1-RMD-956 Result

Status: Done.

- The payment page scrolls its error box into view whenever an attempt produces an error (also the same error again after a retry), so the sticky pay button cannot hide it. Redraws with an unchanged error do not move the page.
- Note: the "tendered amount below the amount due" error from the report can no longer be reached through the button (it is disabled with a hint since an earlier task); the scroll covers every other error on the page.
- Tests: split-payment suite 243/243; 5 mutants killed. Real visibility needs a browser and is not proven by jsdom.
