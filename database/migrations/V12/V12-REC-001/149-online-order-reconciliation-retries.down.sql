-- V12-REC-001: remove the online order reconciliation retry trail.
DROP TABLE IF EXISTS reconciliation.online_order_retry_attempts;
