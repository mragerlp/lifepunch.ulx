# Native cash-grant checks

Run from the repository root:

```powershell
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
dotnet run --project tests/ulx-money-grants/MoneyGrant.Tests.csproj --configuration Release
```

The suite compiles the actual money RPC partial and presentation capabilities with `LIFEPUNCH_PACKAGE`. Only engine, transport, rank lookup and the native `Player.PayHost` boundary are replaced with local fixtures. Fixture balances are read-only, so an add-on-side balance assignment cannot compile. No server or player funds are touched.

It checks current host/caller/target identity, permission and rank checks, amount/reason validation, Cash-only availability, exactly one native call, recipient-scoped receipts, read-only reconciliation, replay handling, and retained caller locks after false/exception/disconnect/lost-link outcomes. Receipt eviction cannot release an unresolved caller lock. Only the newest 128 result receipts are cached: explicitly resubmitting an older successful ID can pay again, so the UI never resubmits a completed or uncertain grant.

A native `true` result proves local cash application only; audit delivery remains unconfirmed. Bank grants stay unavailable in the package build. The non-package strict adapter remains a separate integration path. These fixtures do not prove live audit delivery, menu rendering, reconnect behavior, or host migration. Receipt caches and unresolved locks last only for the current host lifetime; an unresolved grant must be reconciled operationally before any retry after a server restart.
