-- V1-IAM-023: behavioural tightening (docs/domain/authorization-model.md §1,
-- "Behavioural tightening" — the preventive layer no competitor ships).
--
-- The grant service reads a rolling per-user rate for the monetary grant-class
-- permissions (bills.void / bills.comp / bills.discount) straight off
-- identity.authorization_grants. A user whose granted count in the trailing
-- window reaches >= 3x their 30-day baseline is auto-moved to "requires grant":
-- an open row here forces every later request for that (user, permission) to a
-- manager, even actions the role normally auto-approves, until a manager clears
-- it. Every open and every clear is one audit row.

CREATE TABLE identity.behavioural_tightenings (
    tightening_id       UUID          NOT NULL DEFAULT gen_random_uuid(),
    user_id             UUID          NOT NULL,
    permission_code     TEXT          NOT NULL,
    recent_count        INTEGER       NOT NULL,
    baseline_per_window NUMERIC(12, 4) NOT NULL,
    trigger_ratio       NUMERIC(12, 4) NOT NULL,
    triggered_at        TIMESTAMPTZ   NOT NULL DEFAULT now(),
    cleared_at          TIMESTAMPTZ,
    cleared_by_user_id  UUID,
    PRIMARY KEY (tightening_id),
    CONSTRAINT ck_behavioural_tightenings_counts
        CHECK (recent_count >= 0 AND baseline_per_window >= 0 AND trigger_ratio >= 0),
    CONSTRAINT ck_behavioural_tightenings_clear
        CHECK ((cleared_at IS NULL) = (cleared_by_user_id IS NULL)
               AND (cleared_at IS NULL OR cleared_at >= triggered_at))
);

-- At most one open tightening per (user, permission); the gate re-fetches this
-- row on every request.
CREATE UNIQUE INDEX uq_behavioural_tightenings_active
    ON identity.behavioural_tightenings (user_id, permission_code)
    WHERE cleared_at IS NULL;

-- The gate consults the most recent clear so a just-cleared user is not
-- immediately re-tightened by the same trailing window.
CREATE INDEX ix_behavioural_tightenings_cleared
    ON identity.behavioural_tightenings (user_id, permission_code, cleared_at)
    WHERE cleared_at IS NOT NULL;

-- Append-once-clear: a row is born open and updated exactly once to cleared. No
-- re-open, no delete, no mutation of the trigger evidence.
CREATE OR REPLACE FUNCTION identity.enforce_behavioural_tightening_transition()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'identity.behavioural_tightenings rows cannot be deleted.';
    END IF;
    IF OLD.cleared_at IS NOT NULL THEN
        RAISE EXCEPTION 'behavioural tightening % is already cleared.', OLD.tightening_id;
    END IF;
    IF NEW.cleared_at IS NULL THEN
        RAISE EXCEPTION 'a behavioural tightening may only change by being cleared.';
    END IF;
    IF NEW.tightening_id <> OLD.tightening_id
       OR NEW.user_id <> OLD.user_id
       OR NEW.permission_code <> OLD.permission_code
       OR NEW.recent_count <> OLD.recent_count
       OR NEW.baseline_per_window <> OLD.baseline_per_window
       OR NEW.trigger_ratio <> OLD.trigger_ratio
       OR NEW.triggered_at <> OLD.triggered_at THEN
        RAISE EXCEPTION 'only the clear fields of a behavioural tightening may change.';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_behavioural_tightenings_transition
BEFORE UPDATE OR DELETE ON identity.behavioural_tightenings
FOR EACH ROW EXECUTE FUNCTION identity.enforce_behavioural_tightening_transition();
