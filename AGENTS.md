# LifePunch ULX agent guide

This repository owns the standalone `lifepunch.ulx` add-on for the official `dxura.rp` parent. Read `README.md` and preserve the existing source ownership notices. Other agents may be editing this checkout; keep changes scoped and do not revert their work.

## Native integration

- `ulx.sbproj` defines `SANDBOX;ADDON;LIFEPUNCH_PACKAGE`. Verify the installed parent assembly and record its SHA-256 before relying on an API. A local compatibility shim is not evidence that the published parent exposes that API.
- Keep authoritative permission, rank, connection, amount and replay checks on the host. Menu visibility and displayed rank names do not authorize an action.
- Package money grants use native `Player.PayHost` exactly once for Cash. Do not assign player balances directly. Bank is unavailable; native cash success does not confirm audit delivery. UNKNOWN results keep the caller lock and cannot be retried automatically. Receipts and locks last for the host lifetime only.
- Preserve the non-package strict adapter unless its behavior is explicitly part of the task. Keep secrets out of source, logs and test output.

## Checks and source custody

- Run the relevant commands in `.github/workflows/checks.yml`; the money suite is `dotnet run --project tests/ulx-money-grants/MoneyGrant.Tests.csproj --configuration Release`.
- Keep `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false` in build subprocesses. Do not persist temporary tools or build directories into the Windows PATH.
- Compile both C# and Razor against the installed official parent before staging. Record exit codes and warnings. Source fixtures and an offline compile do not prove rendering, live payment, audit delivery, reconnect or host migration.
- Stage the complete reviewed `Code` tree, including new partials such as `StaffMenuBridgeService.Money.cs` and `StaffMoneyGrantCapabilities.cs`. Test projects that link individual source files must also include their shared dependencies.
- Preserve existing line endings. For this CRLF source tree, use `git -c core.whitespace=cr-at-eol diff --check` and report any remaining whitespace errors.

## QA and publication

- Keep the source commit, s&box package revision, Portal add-on revision, saved gamemode pin and actually mounted server/client revision distinct. A push does not publish; saving a Portal pin does not prove running code refreshed.
- Use a clean staged project and the native publisher preview. Verify source hashes, the compiled archive and stylesheet manifest, then verify the accepted download before updating QA. Preserve and read back the world/player save before any required restart.
- Private QA access and package visibility are separate. `lifepunch.ulx` has been publicly released; `IncludeSourceFiles=false` is not privacy. Check current package visibility before publication and do not change it implicitly.
- Apply candidate pins to the designated QA environment. Preserve production pins unless the active task includes promotion. Record the prior pin for rollback and check the mounted revision after restart/rejoin.
- End a checkpoint with changed files, exact check outcomes and remaining runtime gaps. Update these instructions when an accepted workflow changes.
