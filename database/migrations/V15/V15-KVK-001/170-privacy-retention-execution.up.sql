-- V15-KVK-001: retention execution. A versioned policy holds one retention
-- window per data class; a run selects the records whose window has elapsed
-- and writes one work item per record for the anonymization workflow. Policy
-- versions, runs and run items are immutable audit; a work item only ever
-- goes Pending -> Done, and never while its record is under a legal hold.

CREATE SCHEMA IF NOT EXISTS privacy;

CREATE TABLE IF NOT EXISTS privacy.retention_policies (
    policy_version  INTEGER     NOT NULL PRIMARY KEY CHECK (policy_version > 0),
    published_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    published_by    UUID        NULL,
    note            TEXT        NOT NULL CHECK (length(btrim(note)) > 0)
);

CREATE TABLE IF NOT EXISTS privacy.retention_rules (
    policy_version   INTEGER NOT NULL REFERENCES privacy.retention_policies (policy_version),
    data_class       TEXT    NOT NULL
        CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier')),
    retention_years  INTEGER NOT NULL CHECK (retention_years BETWEEN 1 AND 30),
    PRIMARY KEY (policy_version, data_class)
);

CREATE TABLE IF NOT EXISTS privacy.legal_holds (
    hold_id      UUID        NOT NULL PRIMARY KEY,
    data_class   TEXT        NOT NULL
        CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier')),
    subject_id   UUID        NOT NULL,
    reason       TEXT        NOT NULL CHECK (length(btrim(reason)) > 0),
    placed_by    UUID        NOT NULL,
    placed_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    released_by  UUID        NULL,
    released_at  TIMESTAMPTZ NULL,
    CONSTRAINT ck_legal_holds_release_pair CHECK ((released_by IS NULL) = (released_at IS NULL))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_legal_holds_active
    ON privacy.legal_holds (data_class, subject_id) WHERE released_at IS NULL;

CREATE TABLE IF NOT EXISTS privacy.retention_runs (
    run_id          UUID        NOT NULL PRIMARY KEY,
    policy_version  INTEGER     NOT NULL REFERENCES privacy.retention_policies (policy_version),
    as_of           TIMESTAMPTZ NOT NULL,
    requested_by    TEXT        NOT NULL CHECK (length(btrim(requested_by)) > 0),
    started_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    item_count      INTEGER     NOT NULL CHECK (item_count >= 0)
);

CREATE TABLE IF NOT EXISTS privacy.retention_run_items (
    run_id      UUID        NOT NULL REFERENCES privacy.retention_runs (run_id),
    data_class  TEXT        NOT NULL,
    subject_id  UUID        NOT NULL,
    due_since   TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (run_id, data_class, subject_id)
);

CREATE TABLE IF NOT EXISTS privacy.retention_work_items (
    data_class      TEXT        NOT NULL
        CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier')),
    subject_id      UUID        NOT NULL,
    run_id          UUID        NOT NULL REFERENCES privacy.retention_runs (run_id),
    policy_version  INTEGER     NOT NULL REFERENCES privacy.retention_policies (policy_version),
    due_since       TIMESTAMPTZ NOT NULL,
    status          TEXT        NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Done')),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at    TIMESTAMPTZ NULL,
    completed_by    TEXT        NULL,
    PRIMARY KEY (data_class, subject_id),
    CONSTRAINT ck_work_items_done_pair CHECK ((status = 'Done') = (completed_at IS NOT NULL AND completed_by IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_retention_work_items_pending
    ON privacy.retention_work_items (data_class, due_since) WHERE status = 'Pending';

-- Immutable audit: policy versions, rules, runs and run items never change.
CREATE OR REPLACE FUNCTION privacy.refuse_audit_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION '% rows are immutable', TG_TABLE_NAME
        USING ERRCODE = 'integrity_constraint_violation';
END;
$$;

DROP TRIGGER IF EXISTS tr_retention_policies_immutable ON privacy.retention_policies;
CREATE TRIGGER tr_retention_policies_immutable BEFORE UPDATE OR DELETE ON privacy.retention_policies
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_audit_change();
DROP TRIGGER IF EXISTS tr_retention_rules_immutable ON privacy.retention_rules;
CREATE TRIGGER tr_retention_rules_immutable BEFORE UPDATE OR DELETE ON privacy.retention_rules
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_audit_change();
DROP TRIGGER IF EXISTS tr_retention_runs_immutable ON privacy.retention_runs;
CREATE TRIGGER tr_retention_runs_immutable BEFORE UPDATE OR DELETE ON privacy.retention_runs
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_audit_change();
DROP TRIGGER IF EXISTS tr_retention_run_items_immutable ON privacy.retention_run_items;
CREATE TRIGGER tr_retention_run_items_immutable BEFORE UPDATE OR DELETE ON privacy.retention_run_items
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_audit_change();

-- A policy is complete: one rule for every data class, checked at commit.
CREATE OR REPLACE FUNCTION privacy.assert_policy_complete() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF (SELECT count(*) FROM privacy.retention_rules WHERE policy_version = NEW.policy_version) <> 5 THEN
        RAISE EXCEPTION 'retention policy % must define a window for every data class', NEW.policy_version
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS tr_retention_policies_complete ON privacy.retention_policies;
CREATE CONSTRAINT TRIGGER tr_retention_policies_complete
    AFTER INSERT ON privacy.retention_policies
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION privacy.assert_policy_complete();

-- A hold is only ever released, never edited or removed.
CREATE OR REPLACE FUNCTION privacy.guard_legal_hold_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' OR OLD.released_at IS NOT NULL
       OR (NEW.hold_id, NEW.data_class, NEW.subject_id, NEW.reason, NEW.placed_by, NEW.placed_at)
          IS DISTINCT FROM (OLD.hold_id, OLD.data_class, OLD.subject_id, OLD.reason, OLD.placed_by, OLD.placed_at) THEN
        RAISE EXCEPTION 'legal holds can only be released' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_legal_holds_guard ON privacy.legal_holds;
CREATE TRIGGER tr_legal_holds_guard BEFORE UPDATE OR DELETE ON privacy.legal_holds
    FOR EACH ROW EXECUTE FUNCTION privacy.guard_legal_hold_change();

-- A legal hold blocks the mutation: no work item is created for a held record
-- and a work item of a held record cannot be completed.
CREATE OR REPLACE FUNCTION privacy.guard_work_item_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'retention work items are never deleted' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF TG_OP = 'UPDATE' AND (OLD.status <> 'Pending' OR NEW.status <> 'Done'
       OR (NEW.data_class, NEW.subject_id, NEW.run_id, NEW.policy_version, NEW.due_since, NEW.created_at)
          IS DISTINCT FROM (OLD.data_class, OLD.subject_id, OLD.run_id, OLD.policy_version, OLD.due_since, OLD.created_at)) THEN
        RAISE EXCEPTION 'a retention work item can only move from Pending to Done' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF EXISTS (SELECT 1 FROM privacy.legal_holds h
               WHERE h.data_class = NEW.data_class AND h.subject_id = NEW.subject_id AND h.released_at IS NULL) THEN
        RAISE EXCEPTION 'record % is under a legal hold', NEW.subject_id USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_retention_work_items_guard ON privacy.retention_work_items;
CREATE TRIGGER tr_retention_work_items_guard BEFORE INSERT OR UPDATE OR DELETE ON privacy.retention_work_items
    FOR EACH ROW EXECUTE FUNCTION privacy.guard_work_item_change();

-- Policy 1 is the approved data inventory: staff accounts 1 year after they
-- go inactive, order and reservation notes 5 years, customer and supplier
-- records 10 years after their last activity.
INSERT INTO privacy.retention_policies (policy_version, published_by, note)
VALUES (1, NULL, 'Approved data inventory')
ON CONFLICT (policy_version) DO NOTHING;

INSERT INTO privacy.retention_rules (policy_version, data_class, retention_years) VALUES
    (1, 'StaffAccount', 1),
    (1, 'OrderNotes', 5),
    (1, 'ReservationReason', 5),
    (1, 'CustomerProfile', 10),
    (1, 'Supplier', 10)
ON CONFLICT (policy_version, data_class) DO NOTHING;
