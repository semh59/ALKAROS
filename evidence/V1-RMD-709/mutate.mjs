import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const p = (rel) => `${root}/${rel}`;
const store = p("src/Host/Experience/Catalog/CatalogManagementStore.cs");
const product = p("src/Modules/Catalog/ProductCatalog/Product.cs");
const repo = p("src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs");
const endpoints = p("src/Host/Experience/Catalog/CatalogManagementEndpoints.cs");
const ui = p("src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx");
const api = p("src/Clients/PosTerminal/src/features/catalog/catalogApi.ts");
const log = p("evidence/V1-RMD-709/mutations.log");
const host = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj";
const mod = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test tests/Modules/Catalog/ProductCatalog";
const web = 'cd src/Clients/PosTerminal && export PATH="$PATH:/c/Program Files/nodejs" && npx vitest run src/features/catalog';

const mutants = [
  { id: "S1", file: store, from: "        await EnsureActiveTaxProfileAsync(request.TaxProfileId, cancellationToken);\n        var updated", to: "        var updated", cmd: host, note: "details update skips the tax profile check" },
  { id: "S2", file: product, from: "Id, sku, name, ProductType, StockMode, categoryId, taxProfileId, description,\n        PrinterRoutePolicy, displayOrder", to: "Id, sku, name, ProductType, StockMode, CategoryId, taxProfileId, description,\n        PrinterRoutePolicy, displayOrder", cmd: host, note: "category not updated" },
  { id: "S3", file: repo, from: "                description = @description,\n", to: "                description = @description,\n                current_price = @current_price,\n", cmd: mod, note: "product update writes the cached price again" },
  { id: "S4", file: endpoints, from: "var updated = await store.UpdateProductDetailsAsync(productId, request, cancellationToken);\n            return updated is null ? Results.NotFound() : Results.Ok(updated);", to: "var updated = await store.UpdateProductDetailsAsync(productId, request, cancellationToken);\n            return Results.Ok(updated);", cmd: host, note: "unknown product answers 200" },
  { id: "S5", file: store, from: "NormalizeRequired(request.Sku, 100, nameof(request.Sku)),\n            NormalizeRequired(request.Name, 300, nameof(request.Name)),\n            request.CategoryId,", to: "product.Sku,\n            NormalizeRequired(request.Name, 300, nameof(request.Name)),\n            request.CategoryId,", cmd: host, note: "sku change ignored" },
  { id: "C1", file: api, from: "request(`/products/${productId}/details`", to: "request(`/products/${productId}/detail`", cmd: web, note: "wrong details route" },
  { id: "C2", file: ui, from: "onEdit={canManage && onUpdateProduct ? openProductEditor : undefined}", to: "onEdit={openProductEditor}", cmd: web, note: "edit offered without capability" },
  { id: "C3", file: ui, from: "if (!editing && (kind === \"products\" || kind === \"prices\")", to: "if ((kind === \"products\" || kind === \"prices\")", cmd: web, note: "edit demands a price" },
  { id: "C4", file: ui, from: "description: editing.description, displayOrder: editing.displayOrder", to: "description: null, displayOrder: 0", cmd: web, note: "description and order wiped on edit" },
  { id: "C5", file: ui, from: "{!editing && <><SelectField label=\"Ürün tipi\"", to: "{true && <><SelectField label=\"Ürün tipi\"", cmd: web, note: "edit shows product type" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(from, m.to.replace(/\n/g, nl)));
  let result;
  try { execSync(m.cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA|error TS/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
