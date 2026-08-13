# ENV-001 evidence - .NET SDK 10.0.302 repair

Date: 2026-08-03
Machine: win32 (Windows 10.0.26200)

## Finding

- SDK 10.0.302 at C:\Program Files\dotnet\sdk\10.0.302 was missing
  Sdks\Microsoft.NET.Sdk\Targets\Microsoft.NET.Sdk.DefaultItems.Shared.targets
  (imported by Microsoft.NET.Sdk.DefaultItems.targets line 71).
- Every project failed with MSB4019 (39 errors, 0 warnings) on
  'dotnet build ALKAROS.slnx --no-restore --warnaserror'.
- Subsequent build revealed a second missing import:
  Microsoft.NET.ComposeStore.targets -> SDK install was broadly corrupt.

## Repair

- Downloaded official SDK payload:
  <https://dotnetcli.blob.core.windows.net/dotnet/Sdk/10.0.302/dotnet-sdk-10.0.302-win-x64.zip>
  (sha256 via zip integrity: extracted 3792 sdk/ entries).
- Moved corrupt C:\Program Files\dotnet\sdk\10.0.302 -> 10.0.302.broken (kept as backup).
- Extracted the full official sdk/ tree to C:\Program Files\dotnet\sdk\10.0.302.

## Verification

Command: dotnet build ALKAROS.slnx --no-restore --warnaserror
Result: 0 warnings, 0 errors, build succeeded (17.87s).

Command: dotnet --version
Result: 10.0.302
