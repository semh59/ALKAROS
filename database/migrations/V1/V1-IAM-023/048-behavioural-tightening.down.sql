-- Reverse of 048.
DROP TRIGGER IF EXISTS trg_behavioural_tightenings_transition
    ON identity.behavioural_tightenings;
DROP FUNCTION IF EXISTS identity.enforce_behavioural_tightening_transition();
DROP TABLE IF EXISTS identity.behavioural_tightenings;
