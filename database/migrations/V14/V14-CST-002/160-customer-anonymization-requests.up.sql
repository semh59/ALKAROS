CREATE TABLE IF NOT EXISTS customer_data.anonymization_requests (
    id              UUID          NOT NULL PRIMARY KEY,
    customer_id     UUID          NOT NULL,
    status          TEXT          NOT NULL
        CHECK (status IN ('RetentionBlocked', 'Pending', 'Anonymized')),
    requested_at    TIMESTAMPTZ   NOT NULL,
    requested_by    TEXT          NULL,
    blocked_reason  TEXT          NULL,
    anonymized_at   TIMESTAMPTZ   NULL,
    row_version     INTEGER       NOT NULL DEFAULT 1,
    CHECK ((status = 'RetentionBlocked') = (blocked_reason IS NOT NULL)),
    CHECK ((status = 'Anonymized') = (anonymized_at IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_anonymization_requests_customer_id
    ON customer_data.anonymization_requests (customer_id);
