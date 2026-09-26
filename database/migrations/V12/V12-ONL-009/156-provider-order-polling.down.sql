-- V12-ONL-009 rollback: polling state is dropped; events already polled stay in the inbox.
DROP TABLE IF EXISTS online_ordering.provider_poll_state;
