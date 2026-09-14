# ULX release checkpoint — NOT PUBLISHED

Bloodwave authorized GitHub backup and handoff on 2026-09-14. Release branch is `publish`; this checkpoint stays on `codex/ulx-polish-20260914` until compatibility and release gates pass.

`shipping/` and `lifepunchulx-CANDIDATE-menu-save.zip` contain the latest reviewed runtime candidate, including dedicated client-local menu lifetime/close correction and animated website Save feedback. The package `Code/` directory retains its separate harness variant and does NOT include those final off-product changes. Do not confuse a successful harness build with the shipping variant.

Archive SHA256: 2C12FD2E56F81CA87A0DBBA242475E8280AADB50D32AC251A36D522E1B7B8311
Archive bytes: 149923. Entries: 34. Exactly three replacements from the prior reviewed release; 31 unchanged. See manifest.json. Shipping SCSS preserves its flattened footer import.

Source review passed for menu lifetime and Save feedback. Compilation, remote runtime and visual proof of those new changes remain UNVERIFIED. No source was hot-written into the live workbench. No Portal upload, publication, game-mode pin change or server restart occurred.

## Blocking work

1. Obtain the official destination server's exact loaded DXRP parent DLL, matching revision/manifest and compile defines. Cached published parent lacks multiple native APIs required by shipping (see parent-compatibility report). Do not upload all workbench native code as a shortcut.
2. Portal/runtime permissions disagree: Cornerman's exact Super Admin rank is wildcard/All Servers on Portal and recognized on host, but host denies inventory.view, player.pocket.view, portal.audit.view and command.waypoint.use; command.ban is true. Diagnose snapshot freshness/resolution before changing grants. Owner-target protection is separate.
3. Staff must see Audit, Waypoints, Observe and catalog commands; denied operations stay disabled with an explanation. Remove Observe's hardcoded order-10 floor. Keep Portal read and mutation authorization, including ban/unban, on the host. Removing visibility filters alone is UNSAFE: existing IsEnabled relies on them and needs explicit permission checks at UI/click/confirmation/dispatch.
4. Remote Audit currently has NO transport. Add a bounded requester-only host snapshot with authorization, correlation and permission-revocation-safe cache. Do not claim revealing the tab solves it.
5. Adopt/compile exact reviewed source only with Play stopped. Never save scene. Test on the real server with Cornerman per Bloodwave's latest decision, after build compatibility is proved. Publish existing Portal addon lifepunchulx, public identity lifepunch.lifepunchulx. Do not create a third package or modify hidden lifepunch.ulx.

Workbench D:\Cavelux\dxrp-workbench has separate unrelated dirty work and native X-ray dependency changes; this checkpoint does not back up that entire repository. No blanket merge/cleanup. Native delivery and exact server-parent proof remain separate.