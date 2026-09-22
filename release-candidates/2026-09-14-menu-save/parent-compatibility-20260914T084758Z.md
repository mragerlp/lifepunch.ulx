# ULX parent compatibility

The current ULX archive is locally reviewed and visually accepted. Publication remains pending because its shipping source requires DXRP APIs that are absent from the cached published parent. The running server's exact parent has not been obtained, so its compatibility is unverified.

## Candidate

- Archive: `footer-clip-candidate-20260914T081734Z/lifepunchulx-CANDIDATE-footer-fix-20260914.zip`
- SHA-256: `0B9C01CB9C5FFF8CF8443E43BF779B8AC3A474EFD9A1ECC816C7535906E7CCD4`
- Size: 149,317 bytes; 34 entries, 572,687 uncompressed bytes.
- Only ULX and its required shared source/styles are included. No weapons, assets, native DXRP files, dev helpers or project configuration.

## Cached parent evidence

Read-only PE metadata inspection of `D:\Cavelux\lifepunch-ulx-package\.sbox\bin\package.dxura.rp.dll` found:

- SHA-256: `07FF2D4F470D0840C582ECA0DA48774C4C7EDB13D51C9AA2A5EA8717D2D9CBF6`
- Size: 4,346,880 bytes.
- Assembly version: `0.0.122.0`.
- MVID: `14215828-fbf1-4bf8-8295-9541e7f397f5`.

These identify the local cached DLL, not the running server's package revision.

The shipping/default branch directly uses the following APIs absent from this cache:

| Area | Required APIs |
| --- | --- |
| X-ray | `Dxura.RP.Game.Commands.XrayCommand.IsActive` |
| Authorization and audit | `ServerApiLink`, `SyntheticActorRegistry`, `LocalAuditStore` |
| Money | `StrictMoneyMutationResult`, `Player.PayHostStrictAudited` |
| Strict store operations | `ServerApiClient.ReadStoreList`, `ReadStoreValue`, `TryDeleteStoreStrict`, `TrySetStoreStrict` |
| Inspection failures | `PocketSystem.AdminViewIsUnavailable`, `PlayerSanctionHistorySystem.CurrentRequestRefused`, `CurrentRequestFailed` |

The existing isolated package build intentionally omits the X-ray reader and uses package-specific service branches. The unchanged shipping X-ray reader remains compiled with `LIFEPUNCH_PACKAGE`; the successful harness used a different Host variant without it. Its success does not establish compatibility for the exact shipping source. The local editor contains newer native code and likewise cannot prove the destination build.

All listed missing definitions are native DXRP source, not omitted addon support files. Local definitions are in `Api/ServerApiLink.cs`, `System/SyntheticActorRegistry.cs`, `Api/LocalAuditStore.cs`, `Api/ServerApiClient.Core.cs`, `Player/Player.Roleplay.cs`, `Api/ServerApiClient.Store.cs`, `System/Player/PocketSystem.cs` and `System/Player/PlayerSanctionHistorySystem.cs`, relative to `game/Code`. These files are tracked and clean in the workbench. The strict money result/method and strict store-write methods are `internal`; the exact shipping build must prove valid assembly visibility as well as member existence.

## Required next step

Obtain a readable copy of the destination server's exact loaded `package.dxura.rp.dll`, matching package revision/manifest and compiler defines. If an exact revision is available, fetch that revision's manifest rather than assuming the latest package is the loaded package. No server permission changes are needed to inspect an exported copy.

Check the required APIs and compile the unchanged 34-file shipping source against that parent using the actual Portal defines. Separately verify native X-ray cleanup from source/build provenance or an appropriate runtime check. The approved local X-ray file is a bounded maintainer handoff; it alone does not resolve the broader compatibility requirements. Do not deploy the whole workbench to supply missing APIs.

After compatibility is established, create a Code revision on the existing `lifepunchulx` Portal addon, retain the `lifepunch.lifepunchulx` collection identity, verify the intended game-mode pin and server delivery, then run the requested two-client checks on the real LifePunch server.

## Destination observation

The current Portal observation showed the official LifePunch server active, zero of 70 players, game mode `lifepunch.dxrp` and engine `26.09.08e`. The engine version does not identify the loaded DXRP source. The authorized remote account could not read the server checkout. No alternate account, elevation, ACL change, upload, revision mutation or server restart was performed.

References: [DXRP revision publishing](https://dxrp.net/docs/developers/publishing/publishing-addons), [s&box package revision API](https://sbox.game/api/Sandbox.Package.IRevision).
