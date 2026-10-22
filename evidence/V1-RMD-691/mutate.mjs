import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const wp = "src/Host/Experience/WebPush/";
const policy = wp + "PushEndpointPolicy.cs";
const store = wp + "PushSubscriptionStore.cs";
const sender = wp + "WebPushSender.cs";
const dispatch = wp + "WebPushDispatchService.cs";
const experience = wp + "WebPushExperience.cs";
const crypto = wp + "WebPushCrypto.cs";
const waiterDispatch = "src/Host/Experience/WaiterNotifications/WaiterNotificationDispatch.cs";
const kitchen = "src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs";
const pushJs = "src/Clients/WaiterPwa/wwwroot/js/push.js";
const appJs = "src/Clients/WaiterPwa/wwwroot/waiter-app.js";
const webPushTests = "tests/Host/Experience/WebPush/ALKAROS.Host.Experience.WebPush.Tests.csproj";
const kitchenTests = "tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj";
const mutants = [
  { id: "M1", file: policy, from: "&& uri.IsDefaultPort", to: "" },
  { id: "M2", file: policy, from: "&& uri.UserInfo.Length == 0", to: "" },
  { id: "M3", file: policy, from: "&& uri.HostNameType == UriHostNameType.Dns", to: "" },
  { id: "M4", file: policy, from: "&& uri.Host.Contains('.', StringComparison.Ordinal)", to: "" },
  { id: "M5", file: policy, from: "endpoint.Length <= MaximumEndpointLength\n", to: "endpoint.Length <= MaximumEndpointLength + 1\n" },
  { id: "M6", file: policy, from: "bytes[1] is >= 16 and <= 31", to: "bytes[1] is >= 16 and <= 30" },
  { id: "M7", file: policy, from: "bytes[1] is >= 64 and <= 127", to: "bytes[1] is >= 64 and <= 126" },
  { id: "M8", file: policy, from: "(bytes[0] & 0xFE) == 0xFC", to: "(bytes[0] & 0xFF) == 0xFC" },
  { id: "M9", file: policy, from: "            address = address.MapToIPv4();", to: "" },
  { id: "M10", file: policy, from: "AllowAutoRedirect = false,", to: "AllowAutoRedirect = true," },
  { id: "M11", file: policy, from: "        ConnectCallback = ConnectPublicOnlyAsync,\n", to: "" },
  { id: "M12", file: policy, from: ".Where(IsPublicAddress)", to: ".Where(_ => true)" },
  { id: "M13", file: store, from: "OR notifications.push_subscriptions.user_id = EXCLUDED.user_id;", to: "OR TRUE;" },
  { id: "M14", file: store, from: "WHERE notifications.push_subscriptions.terminal_id = EXCLUDED.terminal_id", to: "WHERE FALSE" },
  { id: "M15", file: store, from: "OFFSET @maximum);", to: "OFFSET @maximum + 1);" },
  { id: "M16", file: store, from: "ORDER BY created_at DESC, subscription_id", to: "ORDER BY created_at ASC, subscription_id" },
  { id: "M17", file: store, from: "                created_at = now(),\n", to: "" },
  { id: "M18", file: store, from: "JOIN identity.users u ON u.user_id = s.user_id AND u.active;", to: "JOIN identity.users u ON u.user_id = s.user_id;" },
  { id: "M19", file: store, from: "JOIN identity.users u ON u.user_id = s.user_id AND u.active\n            WHERE s.user_id = @user_id;", to: "JOIN identity.users u ON u.user_id = s.user_id\n            WHERE s.user_id = @user_id;" },
  { id: "M20", file: store, from: "WHERE endpoint = @endpoint AND failure_count >= @maximum;", to: "WHERE endpoint = @endpoint AND failure_count > @maximum;" },
  { id: "M21", file: store, from: "SET failure_count = failure_count + 1\n            WHERE endpoint = @endpoint;\n            DELETE", to: "SET failure_count = failure_count\n            WHERE endpoint = @endpoint;\n            DELETE" },
  { id: "M22", file: store, from: "SET private_key_envelope = @private_key_envelope, private_key = NULL", to: "SET private_key_envelope = @private_key_envelope" },
  { id: "M23", file: store, from: "if (envelope is not null)\n            return new VapidKeyPair(publicKey, Open(envelope), subject);", to: "if (envelope is not null && plaintextPrivateKey is not null)\n            return new VapidKeyPair(publicKey, plaintextPrivateKey, subject);" },
  { id: "M24", file: sender, from: "MaximumConsecutiveFailures = 10;", to: "MaximumConsecutiveFailures = 11;" },
  { id: "M25", file: sender, from: "MaximumParallelSends = 8;", to: "MaximumParallelSends = 1;" },
  { id: "M26", file: sender, from: "if (ex is HttpRequestException or TaskCanceledException)\n                await MarkFailureQuietlyAsync", to: "if (ex is TaskCanceledException)\n                await MarkFailureQuietlyAsync" },
  { id: "M27", file: dispatch, from: "                LogDeliveryFailed(_logger, ex);", to: "                _ = ex;" },
  { id: "M28", file: dispatch, from: "Enqueue(cancellationToken => _sender.SendToUserOrBroadcastAsync(message, userId, cancellationToken));", to: "Enqueue(cancellationToken => _sender.BroadcastAsync(message, cancellationToken));" },
  { id: "M29", file: waiterDispatch, from: "                push.ToUserOrBroadcast(pushMessage, pushId);", to: "                push.Broadcast(pushMessage);" },
  { id: "M30", file: waiterDispatch, from: "                push.Broadcast(pushMessage);\n        }", to: "                _ = pushMessage;\n        }" },
  { id: "M31", file: waiterDispatch, from: "        if (push is not null && pushMessage is not null)\n        {\n            if (targetUserId is Guid pushId)\n                push.ToUserOrBroadcast(pushMessage, pushId);\n            else\n                push.Broadcast(pushMessage);\n        }\n\n", to: "" , after: "        await recipients.SendAsync(hubMethod, hubPayload, cancellationToken);\n" },
  { id: "M32", file: experience, from: "if (!PushEndpointPolicy.IsAcceptable(request.Endpoint))", to: "if (request.Endpoint.Length == 0 && !PushEndpointPolicy.IsAcceptable(request.Endpoint))" },
  { id: "M33", file: experience, from: "if (!WebPushCrypto.AreValidSubscriptionKeys(request.P256dh, request.Auth))", to: "if (request.Auth.Length == 0 && !WebPushCrypto.AreValidSubscriptionKeys(request.P256dh, request.Auth))" },
  { id: "M34", file: experience, from: "return saved\n                ? Results.NoContent()", to: "return true\n                ? Results.NoContent()" },
  { id: "M35", file: experience, from: "if (!services.Any(descriptor => descriptor.ServiceType == typeof(WebPushDispatchService)))", to: "if (true)" },
  { id: "M36", file: crypto, from: "FromBase64Url(auth).Length == AuthSecretBytes", to: "FromBase64Url(auth).Length >= AuthSecretBytes" },
  { id: "M37", file: experience, from: "new HttpClient(PushEndpointPolicy.CreateHandler())", to: "new HttpClient()" },
  { id: "K2", kind: "kitchen", file: kitchen, from: "tableNumber is null ? $\"{item.ProductNameSnapshot} hazır\" : $\"{tableNumber} masası: {item.ProductNameSnapshot} hazır\",", to: "$\"{item.ProductNameSnapshot} hazır\"," },
  { id: "K1", kind: "kitchen", file: kitchen, from: "$\"alkaros-order-ready-{item.OrderItemId}\"", to: "\"alkaros-order-ready\"" },
  { id: "C1", kind: "py", file: pushJs, from: "permission === 'denied'", to: "permission === 'never'" },
  { id: "C2", kind: "py", file: pushJs, from: "    setPushWanted(true);\n    return true;", to: "    return true;" },
  { id: "C3", kind: "py", file: pushJs, from: "  setPushWanted(false);\n}", to: "}" },
  { id: "C4", kind: "py", file: pushJs, from: "export async function restorePush(force = false) {", to: "export async function restorePush() {" },
  { id: "C5", kind: "py", file: appJs, from: "void refreshPushState().then(() => restorePush());", to: "void refreshPushState();" },
  { id: "C6", kind: "py", file: appJs, from: "      void restorePush(true);\n", to: "" },
  { id: "C7", kind: "py", file: appJs, from: "`alkaros-order-ready-${payload && payload.orderItemId}`", to: "'alkaros-order-ready'" },
];
const only = process.argv[2] ? process.argv[2].split(",") : null;
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  let mutated = normalized.replace(m.from, () => m.to);
  if (m.after) mutated = mutated.replace(m.after, () => m.after + m.from);
  fs.writeFileSync(full, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let out;
  try {
    const kind = m.kind ?? "server";
    const cmd = kind === "py"
      ? `python -m pytest tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py -q -k "push_flow or ready_notification_tag" -p no:cacheprovider`
      : `"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${kind === "kitchen" ? kitchenTests : webPushTests}`;
    out = execSync(cmd, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres", PYTHONIOENCODING: "utf-8" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED|failed/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-691/mutations.log"), log.join("\n") + "\n");
