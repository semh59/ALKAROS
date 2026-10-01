-- V15-KVK-002: the anonymization workflow's own state. One job per retention work item (data class + subject), one
-- immutable checkpoint per finished store step so an interrupted job resumes where it stopped, and an append-only event
-- trail. None of these tables holds personal data: only ids, step keys, counts and error classes.

CREATE TABLE IF NOT EXISTS privacy.anonymization_jobs (
    job_id        UUID        NOT NULL PRIMARY KEY,
    data_class    TEXT        NOT NULL
        CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier')),
    subject_id    UUID        NOT NULL,
    status        TEXT        NOT NULL CHECK (status IN ('Running', 'Failed', 'Blocked', 'Done')),
    attempts      INTEGER     NOT NULL DEFAULT 1 CHECK (attempts > 0),
    last_error    TEXT        NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at  TIMESTAMPTZ NULL,
    CONSTRAINT ux_anonymization_jobs_subject UNIQUE (data_class, subject_id),
    CONSTRAINT ck_anonymization_jobs_done CHECK ((status = 'Done') = (completed_at IS NOT NULL))
);

CREATE TABLE IF NOT EXISTS privacy.anonymization_checkpoints (
    job_id          UUID        NOT NULL REFERENCES privacy.anonymization_jobs (job_id),
    step_key        TEXT        NOT NULL CHECK (length(btrim(step_key)) > 0),
    fields_changed  INTEGER     NOT NULL CHECK (fields_changed >= 0),
    completed_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (job_id, step_key)
);

CREATE TABLE IF NOT EXISTS privacy.anonymization_events (
    event_id     BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    job_id       UUID        NOT NULL REFERENCES privacy.anonymization_jobs (job_id),
    event_type   TEXT        NOT NULL CHECK (event_type IN ('StepDone', 'StepFailed', 'VerificationFailed', 'Blocked', 'Completed')),
    step_key     TEXT        NULL,
    detail       TEXT        NULL,
    actor        TEXT        NOT NULL CHECK (length(btrim(actor)) > 0),
    occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_anonymization_events_job ON privacy.anonymization_events (job_id, event_id);

-- Checkpoints and events are only ever added; a job is never removed.
DROP TRIGGER IF EXISTS tr_anonymization_checkpoints_immutable ON privacy.anonymization_checkpoints;
CREATE TRIGGER tr_anonymization_checkpoints_immutable BEFORE UPDATE OR DELETE ON privacy.anonymization_checkpoints
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_audit_change();
DROP TRIGGER IF EXISTS tr_anonymization_events_immutable ON privacy.anonymization_events;
CREATE TRIGGER tr_anonymization_events_immutable BEFORE UPDATE OR DELETE ON privacy.anonymization_events
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_audit_change();

CREATE OR REPLACE FUNCTION privacy.refuse_anonymization_job_delete() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'anonymization jobs are never removed' USING ERRCODE = 'integrity_constraint_violation';
END;
$$;

DROP TRIGGER IF EXISTS tr_anonymization_jobs_no_delete ON privacy.anonymization_jobs;
CREATE TRIGGER tr_anonymization_jobs_no_delete BEFORE DELETE ON privacy.anonymization_jobs
    FOR EACH ROW EXECUTE FUNCTION privacy.refuse_anonymization_job_delete();
