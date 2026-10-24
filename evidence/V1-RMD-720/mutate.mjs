import fs from 'node:fs';
import { execSync } from 'node:child_process';
const OI = 'src/Modules/Orders/OrderAggregate/OrderItem.cs';
const OM = 'src/Modules/Orders/OrderAggregate/OrderMath.cs';
const OR = 'src/Modules/Orders/OrderAggregate/Order.cs';
const CO = 'src/Host/Experience/Orders/OrderSubmissionCoordinator.cs';
const SV = 'src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs';
const AGG = 'tests/Modules/Orders/OrderAggregate';
const TD = 'tests/Host/Experience/Orders/TableDraft --filter FullyQualifiedName~FiringACourse';
const VS = 'tests/Host/Experience/Orders/VoidSent';
const M = [
  ['A1-held-not-computed', OR, 'item.CourseNumber.Value != firstCourseInRound', 'item.CourseNumber.Value == firstCourseInRound', AGG],
  ['A1b-first-course-is-max', OR, '.DefaultIfEmpty(int.MinValue)\n            .Min();', '.DefaultIfEmpty(int.MinValue)\n            .Max();', AGG],
  ['A2-discount-limit-off-by-one', OI, 'if (DiscountAmount > lineSubtotal)', 'if (DiscountAmount >= lineSubtotal)', AGG],
  ['A4-comp-dropped-from-totals', OI, 'OrderItemState.Active or OrderItemState.Complimentary;', 'OrderItemState.Active;', AGG],
  ['A5-bankers-rounding', OM, 'Math.Round(value, 2, MidpointRounding.AwayFromZero)', 'Math.Round(value, 2, MidpointRounding.ToEven)', AGG],
  ['T1-duplicate-fire-ticket', CO, 'if (!alreadyPrinted)', 'if (true)', TD],
  ['V4-commit-before-audit', SV, "await AppendAuditAsync(connection, transaction, order.Id, item, command, stockRestored, cancellationToken).ConfigureAwait(false);\n            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);", "await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);\n            await AppendAuditAsync(connection, transaction, order.Id, item, command, stockRestored, cancellationToken).ConfigureAwait(false);", VS],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
for (const [id, file, from, to, target] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const t = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-720/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  try { execSync(`${process.env.HOME}/.dotnet/dotnet.exe test ${target} -nologo`, { stdio: 'pipe', env }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-720/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-720/mutations.log', 'DONE\n');
