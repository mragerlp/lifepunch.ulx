# LifePunch ULX for DXRP

LifePunch ULX adds a staff console to the official `dxura.rp` parent. Open it with `/ulx`, `/menu` or `/lifepunchulx`. The receiving server supplies its players, ranks and permissions through its own DXRP Portal connection.

## Install on your server

1. Open your gamemode in the DXRP Portal. Add the LifePunch ULX addon and select a reviewed revision.
2. Check the addon's s&box package identifier is `lifepunch.ulx`. The November 16 tested package is revision `447388`, built from source commit `9c21f15b82fa343e964c114b45203c740f59ddad`. Portal revision numbers and s&box package revisions identify different artifacts.
3. Save the gamemode and sync it to your test server. After preserving the world, restart the test server when its mounted revision still differs from the saved pin. Verify the incoming package revision in a fresh client's log.
4. Join with an authorized staff account and open `/ulx`. Test the Owner and a restricted staff account before promoting the revision.

[Published s&box addon](https://sbox.game/lifepunch/ulx) · [DXRP operator documentation](https://dxrp.net/docs/operators/setting-up-a-server)

## Permissions and server identity

ULX uses the receiving server's authenticated DXRP configuration. A server token belongs in the host's protected launch configuration. Joining clients obtain permitted display data through host requests.

- Ranks requires `portal.rank.view`. An Owner wildcard satisfies it. An Administrator rank can have a different grant on each network.
- The directory includes global ranks and ranks assigned to the current server, including ranks with zero online holders. Ranks scoped to another server are excluded.
- A denied viewer receives a permission message and zero directory rows. An unavailable directory indicates that ULX could not read a compatible host snapshot or complete the request.
- The current official-parent adapter reads the host's typed, synchronized `RankSystem.Ranks` property through s&box's TypeLibrary. An upstream change to that property's type or synchronization metadata requires a compatibility check.
- Editing the network website requires `lifepunchulx.settings.edit`. Configure your network's own link in the header gear. The package starts with an empty link.
- Native command permissions and targeting still apply to actions requested from the menu.

## Give Money in this source

On the official DXRP parent, Give Money supports carried cash and bank deposits. The host requires an authorized server, an online target, the issuer's `economy.manage` permission, the right to target that rank, and an audit reason. The native payment runs once per accepted request.

**Cash** produces a local payment confirmation. Portal audit delivery is asynchronous. **Bank** checks the player's balance with the DXRP service before sending the payment. To report success, the service and the host must both show the expected balance after payment on the same server, tenant and player connection. The bank result confirms the observed balance. DXRP does not provide a per-grant transaction receipt.

After an uncertain payment, both the issuer and recipient are locked against new grants for the host session. Recheck can read the current bank balance when the original server and permissions still match. It makes no payment and cannot settle an uncertain request. Staff should use the request reference to reconcile the operation before retrying after a restart or host migration. The host caches its newest 128 completed results. The non-package build retains its strict audited adapter.

These are source capabilities, not proof that an existing published revision contains them. The [money-grant suite](tests/ulx-money-grants/README.md) uses isolated native-boundary fixtures and changes no player funds. Live behavior and audit delivery require separate QA evidence.

## Verify a revision

On a staging server, use two clients to check the expected allow and deny results for Ranks, the selected player's X-ray flag, the Appearance color square, hue strip and hex field, and Observe. Reopen the menu and reconnect a regular client to catch missing assets or stale package pins.

The November 16 runtime checks used LifePunch Development, a separate official DXRP QA server on the same network, and Physgun production. A server operated by a different network remains an independent integration test.

## Run source checks

Install .NET 10. From the repository root run the commands in `.github/workflows/checks.yml`. These suites link production source to isolated fixtures and cover permission denial, server scoping, request paging and expiry, package-store results, chat input and duration boundaries. They need no Portal credentials.

The workflow exercises these algorithms. Native compilation against the current parent, stylesheet delivery and two-client rendering require s&box testing. See [Observe and ranks validation](tests/ULX-observe-validation.md).

This repository contains ULX source and tests. Existing source ownership and license notices apply.
