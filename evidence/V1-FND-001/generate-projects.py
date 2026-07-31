#!/usr/bin/env python3
"""Generate remaining csproj files for V1-FND-001 from the project manifest."""
import os

ROOT = r"d:\PROJECT\ALKAROS"
MC = r"..\..\BuildingBlocks\ModuleComposition\ALKAROS.ModuleComposition.csproj"
ORDERS = r"..\Orders\ALKAROS.Orders.csproj"
BILLING = r"..\Billing\ALKAROS.Billing.csproj"
PAYMENTS = r"..\Payments\ALKAROS.Payments.csproj"
CATALOG = r"..\Catalog\ALKAROS.Catalog.csproj"
CASH = r"..\Cash\ALKAROS.Cash.csproj"
ACCOUNTS = r"..\Accounts\ALKAROS.Accounts.csproj"

def write_csproj(path, kind_prop, module_name, refs):
    full = os.path.join(ROOT, path)
    ref_lines = "".join(
        f'    <ProjectReference Include="{r}" />\n' for r in refs
    )
    content = (
        '<Project Sdk="Microsoft.NET.Sdk">\n\n'
        '  <PropertyGroup>\n'
        '    <TargetFramework>net8.0</TargetFramework>\n'
        f'    {kind_prop}\n'
        f'    <RootNamespace>ALKAROS.{module_name}</RootNamespace>\n'
        '  </PropertyGroup>\n\n'
        '  <ItemGroup>\n'
        f'{ref_lines}'
        '  </ItemGroup>\n\n'
        '</Project>\n'
    )
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"wrote {path}")

# Modules referencing only ModuleComposition
simple_modules = [
    ("src/Modules/Notifications/ALKAROS.Notifications.csproj", "Notifications"),
    ("src/Modules/Settings/ALKAROS.Settings.csproj", "Settings"),
    ("src/Modules/Identity/ALKAROS.Identity.csproj", "Identity"),
    ("src/Modules/Reporting/ALKAROS.Reporting.csproj", "Reporting"),
]
for path, name in simple_modules:
    write_csproj(path, "<ALKAROSModule>true</ALKAROSModule>", name, [MC])

# Modules with additional dependencies
write_csproj("src/Modules/Billing/ALKAROS.Billing.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Billing", [ORDERS, MC])
write_csproj("src/Modules/Payments/ALKAROS.Payments.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Payments", [BILLING, MC])
write_csproj("src/Modules/Kitchen/ALKAROS.Kitchen.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Kitchen", [ORDERS, MC])
write_csproj("src/Modules/Cash/ALKAROS.Cash.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Cash", [PAYMENTS, MC])
write_csproj("src/Modules/Fiscal/ALKAROS.Fiscal.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Fiscal", [PAYMENTS, BILLING, MC])
write_csproj("src/Modules/Inventory/ALKAROS.Inventory.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Inventory", [CATALOG, MC])
write_csproj("src/Modules/Accounts/ALKAROS.Accounts.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Accounts", [BILLING, PAYMENTS, MC])
write_csproj("src/Modules/Reconciliation/ALKAROS.Reconciliation.csproj", "<ALKAROSModule>true</ALKAROSModule>", "Reconciliation", [PAYMENTS, CASH, ACCOUNTS, MC])

# Clients
write_csproj("src/Clients/Cashier/ALKAROS.Cashier.csproj", "<ALKAROSClient>true</ALKAROSClient>", "Cashier", [MC])
write_csproj("src/Clients/Waiter/ALKAROS.Waiter.csproj", "<ALKAROSClient>true</ALKAROSClient>", "Waiter", [MC])

# Integrations
for name in ["Hugin", "Qnb", "Yemeksepeti", "MealCard", "QrRelay"]:
    write_csproj(f"src/Integrations/{name}/ALKAROS.{name}.csproj", "<ALKAROSIntegration>true</ALKAROSIntegration>", name, [MC])

print("all done")