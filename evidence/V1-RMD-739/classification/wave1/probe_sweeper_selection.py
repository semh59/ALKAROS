from pathlib import Path
import hashlib
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'src/Host/Experience/Billing/BillClosureSweeper.cs'
text = SOURCE.read_text(encoding='utf-8')
start = text.index('            SELECT b.bill_id')
end = text.index(';', start) + 1
query = text[start:end].replace('billing.bills', 'pg_temp.probe_bills').replace('payments.payment_allocations', 'pg_temp.probe_allocations').replace('@grace', "interval '0 seconds'").replace('{BatchSize}', '200')
assert 'ORDER BY b.updated_at DESC' in query and 'LIMIT 200' in query
sql = r'''\set ON_ERROR_STOP on
BEGIN;
CREATE TEMP TABLE probe_bills (bill_id integer PRIMARY KEY, status text NOT NULL, updated_at timestamptz NOT NULL, closable boolean NOT NULL);
CREATE TEMP TABLE probe_allocations (bill_id integer NOT NULL, allocated_at timestamptz NOT NULL);
INSERT INTO probe_bills VALUES (1, 'Open', now() - interval '2 days', true);
INSERT INTO probe_bills SELECT i, 'Open', now() - interval '1 day' + i * interval '1 second', false FROM generate_series(2,202) i;
INSERT INTO probe_allocations SELECT bill_id, now() - interval '1 hour' FROM probe_bills;
CREATE TEMP TABLE probe_selected AS
''' + query + '''
DO $$
BEGIN
 IF (SELECT count(*) FROM probe_selected) <> 200 THEN RAISE EXCEPTION 'Unexpected batch size'; END IF;
 IF EXISTS (SELECT 1 FROM probe_selected WHERE bill_id=1) THEN RAISE EXCEPTION 'Old closable bill unexpectedly selected'; END IF;
 IF EXISTS (SELECT 1 FROM probe_selected s JOIN probe_bills b USING(bill_id) WHERE b.closable) THEN RAISE EXCEPTION 'Unexpected closable candidate'; END IF;
END $$;
SELECT 'REPRODUCED: 201 newer unclosable bills exclude older closable bill; selected=200, closable=0' AS result;
TRUNCATE probe_selected;
INSERT INTO probe_selected
''' + query + '''
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM probe_selected WHERE bill_id=1) THEN RAISE EXCEPTION 'Second sweep unexpectedly progresses'; END IF;
END $$;
SELECT 'REPRODUCED: unchanged unclosable batch repeats; old closable bill remains excluded' AS result;
TRUNCATE probe_selected, probe_allocations, probe_bills;
INSERT INTO probe_bills VALUES (1,'Open',now()-interval '1 day',true);
INSERT INTO probe_selected
''' + query + '''
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM probe_selected) THEN RAISE EXCEPTION 'Allocation-free bill unexpectedly selected'; END IF;
END $$;
SELECT 'REPRODUCED: allocation-free closable bill excluded' AS result;
TRUNCATE probe_selected, probe_allocations, probe_bills;
INSERT INTO probe_bills VALUES (1,'Paid',now()-interval '1 day',true);
INSERT INTO probe_allocations VALUES (1,now()-interval '1 hour');
INSERT INTO probe_selected
''' + query + '''
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM probe_selected) THEN RAISE EXCEPTION 'Paid bill unexpectedly selected'; END IF;
END $$;
SELECT 'REPRODUCED: Paid bill excluded regardless of unfinished order side effects' AS result;
ROLLBACK;
'''
command = ['docker', 'exec', '-i', 'alkaros-test-pg', 'psql', '-U', 'postgres', '-d', 'postgres', '-X', '-v', 'ON_ERROR_STOP=1']
result = subprocess.run(command, input=sql, text=True, encoding='utf-8', stdout=subprocess.PIPE, stderr=subprocess.STDOUT, cwd=ROOT)
report = 'COMMAND: ' + ' '.join(command) + '\nSOURCE_SHA256: ' + hashlib.sha256(SOURCE.read_bytes()).hexdigest() + '\nEVIDENCE_TIER: real PostgreSQL selection-query reproduction using isolated TEMP tables; not Host integration\nEXIT_CODE: ' + str(result.returncode) + '\n' + result.stdout
log = Path(__file__).with_name('sweeper-selection-probe.log')
log.write_text(report, encoding='utf-8')
print(report)
sys.exit(result.returncode)
