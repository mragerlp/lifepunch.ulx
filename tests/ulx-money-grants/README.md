# Native money-grant checks

Run from the repository root:

```powershell
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
dotnet run --project tests/ulx-money-grants/MoneyGrant.Tests.csproj --configuration Release
```

The suite compiles the actual money RPC and presentation capabilities with `LIFEPUNCH_PACKAGE`. It replaces engine, transport, rank, bank read-back and payment boundaries with local fixtures. The player balance properties are read-only to the addon.

Checks cover carried cash, bank deposits with matching local and service balances, preflight rejection before dispatch, caller and target locks, query-only reconciliation, interrupted payments and replay. The bank fixture can change service and host balances independently to detect a local-only credit. A later matching balance leaves an uncertain payment unresolved. The live service, Portal audit and host restart are outside this suite.

Run `GrantReceiptNavigation.csx` with the native editor run_csharp tool in a stopped, isolated lifepunch.ulxqa project. It renders and clicks the receipt and menu controls for Cash and Bank, including closing, reopening, read-only recheck and late responses. It sends no payment.
