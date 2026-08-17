CREATE TABLE IF NOT EXISTS billing.bill_adjustments (
    bill_adjustment_id  UUID          NOT NULL,
    bill_id             UUID          NOT NULL,
    bill_item_id        UUID          NULL,
    adjustment_type     TEXT          NOT NULL CHECK (adjustment_type IN ('DiscountPercentage', 'DiscountAmount', 'ServiceFee', 'Kuver', 'Tip', 'CustomFee')),
    calculation_type    TEXT          NOT NULL CHECK (calculation_type IN ('Percentage', 'FixedAmount')),
    rate                NUMERIC(5,2)  NULL,
    amount              NUMERIC(18,2) NOT NULL,
    tax_rate            NUMERIC(5,2)  NOT NULL DEFAULT 0,
    tax_amount          NUMERIC(18,2) NOT NULL DEFAULT 0,
    net_amount          NUMERIC(18,2) NOT NULL,
    gross_amount        NUMERIC(18,2) NOT NULL,
    is_deduction        BOOLEAN       NOT NULL,
    reason              TEXT          NOT NULL,
    authorized_by       UUID          NOT NULL,
    notes               TEXT          NULL,
    created_at          TIMESTAMPTZ   NOT NULL,
    created_by          UUID          NULL,
    row_version         BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (bill_adjustment_id),
    CONSTRAINT fk_bill_adjustments_bill FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id) ON DELETE CASCADE,
    CONSTRAINT fk_bill_adjustments_item FOREIGN KEY (bill_item_id) REFERENCES billing.bill_items (bill_item_id) ON DELETE CASCADE,
    CONSTRAINT chk_adjustment_amount_positive CHECK (amount > 0),
    CONSTRAINT chk_adjustment_net_amount_nonnegative CHECK (net_amount >= 0),
    CONSTRAINT chk_adjustment_gross_amount_nonnegative CHECK (gross_amount >= 0),
    CONSTRAINT chk_adjustment_tax_rate_nonnegative CHECK (tax_rate >= 0),
    CONSTRAINT chk_adjustment_tax_amount_nonnegative CHECK (tax_amount >= 0),
    CONSTRAINT chk_adjustment_rate_range CHECK (rate IS NULL OR (rate > 0 AND rate <= 100))
);

CREATE INDEX IF NOT EXISTS ix_bill_adjustments_bill ON billing.bill_adjustments (bill_id);
CREATE INDEX IF NOT EXISTS ix_bill_adjustments_item ON billing.bill_adjustments (bill_item_id);
