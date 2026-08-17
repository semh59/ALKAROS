CREATE SCHEMA IF NOT EXISTS kitchen;

CREATE TABLE IF NOT EXISTS kitchen.kitchen_tickets (
    id                  UUID         PRIMARY KEY,
    order_id            UUID         NOT NULL REFERENCES orders.orders(order_id),
    ticket_number       VARCHAR(64)  NOT NULL UNIQUE,
    station_id          VARCHAR(64)  NOT NULL,
    status              VARCHAR(32)  NOT NULL CHECK (status IN ('Queued', 'Accepted', 'Preparing', 'Ready', 'Cancelled')),
    row_version         BIGINT       NOT NULL DEFAULT 1,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ  NULL,
    accepted_at         TIMESTAMPTZ  NULL,
    ready_at            TIMESTAMPTZ  NULL,
    cancelled_at        TIMESTAMPTZ  NULL,
    cancellation_reason TEXT         NULL
);

CREATE INDEX IF NOT EXISTS ix_kitchen_tickets_order_id
    ON kitchen.kitchen_tickets (order_id);

CREATE INDEX IF NOT EXISTS ix_kitchen_tickets_station_status
    ON kitchen.kitchen_tickets (station_id, status);

CREATE TABLE IF NOT EXISTS kitchen.kitchen_ticket_items (
    id                    UUID          PRIMARY KEY,
    ticket_id             UUID          NOT NULL REFERENCES kitchen.kitchen_tickets(id) ON DELETE CASCADE,
    order_item_id         UUID          NOT NULL,
    product_id            UUID          NOT NULL,
    product_name_snapshot VARCHAR(255)  NOT NULL,
    quantity              NUMERIC(10,3) NOT NULL CHECK (quantity > 0),
    modifiers_summary     TEXT          NULL,
    notes                 TEXT          NULL,
    status                VARCHAR(32)   NOT NULL CHECK (status IN ('Queued', 'Preparing', 'Ready', 'Served', 'Cancelled')),
    row_version           BIGINT        NOT NULL DEFAULT 1,
    created_at            TIMESTAMPTZ   NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ   NULL,
    ready_at              TIMESTAMPTZ   NULL,
    served_at             TIMESTAMPTZ   NULL,
    cancelled_at          TIMESTAMPTZ   NULL,
    cancellation_reason   TEXT          NULL
);

CREATE INDEX IF NOT EXISTS ix_kitchen_ticket_items_ticket_id
    ON kitchen.kitchen_ticket_items (ticket_id);

CREATE INDEX IF NOT EXISTS ix_kitchen_ticket_items_order_item_id
    ON kitchen.kitchen_ticket_items (order_item_id);
