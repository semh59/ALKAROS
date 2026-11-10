# V1-RMD-920 reviews

Review 1 (qa-backend): exception types survive the submit path (no wrapping, transaction rolled back), no shadowing in the Host default, Orders area unchanged. Found the three new message lines written in a single-byte code page: fixed to UTF-8 (verified, tests rerun 231/231).
Review 2 (qa-api-uclar): same encoding finding; client shows the server message (Turkish) for these codes, no code-specific client handling needed; no docs list per-route error codes (pre-existing gap, not part of this task).
