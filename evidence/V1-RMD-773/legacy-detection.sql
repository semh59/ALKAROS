\pset format unaligned
select 'orders_total', count(*) from orders.orders;
select 'draft_orders_with_sent_row', count(*) from table_mgmt.table_sent_checks s join orders.orders o on o.order_id=s.order_id where o.status='Draft';
select 'draft_orders_total', count(*) from orders.orders where status='Draft';
select 'bills_with_draft_items', count(distinct bi.bill_id), count(*) from billing.bill_items bi join orders.order_items oi on oi.order_item_id=bi.order_item_id where oi.status='Draft';
select 'bills_total', count(*) from billing.bills;
