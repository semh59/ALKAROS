# V1-RMD-992 Result

The end-of-day screens show a separate count of items added at the till without a kitchen ticket ("Mutfağa gitmeden eklenen kalem"): on the close preview, the day panel, the close result and the full report. The count comes from the audit trail (event order.item-added-without-kitchen) inside the business day window; it is not stored on the day record, so no migration.

- red.log: build fails (no KitchenlessAddedCount on the response types). green.log: Reporting EndOfDay HTTP and server figure tests 32 of 32 pass. client.log: closing screen tests 24 of 24, tsc clean, oxlint --max-warnings=0 exit 0, vite build exit 0 (build.log).
- mutations.txt: 4 mutations killed (window end ignored, wrong event name, preview row removed, day row removed), files restored.
