# Cornerman live join findings

The editor bridge confirmed Bloodwave hosting the LifePunch development session and Cornerman connected as a remote client. The mounted project is the workbench. No source, rank, economy or gameplay state was changed during these reads.

## Missing sections

Fresh read-only host RankSystem calls for Cornerman's connected player returned:

- Rank name: Super Admin.
- Rank order: 9.
- `command.waypoint.use`: false.
- `portal.audit.view`: false.
- `command.ban`: true, used as a positive permission control.

ULX gates Waypoints on the first permission and Audit on the second. Observe additionally requires rank order at least 10 in `observe/StaffObserveHost.cs`. Thus the missing sections are consistent with the host's configured grants, not merely an unrefreshed client label. A Super Admin name does not itself grant those permissions. No permission grant was changed.

## Automatic menu and ineffective close

Bloodwave reports that ULX appeared automatically on Cornerman's join and its X cannot close it. The current bridges inspect the editor host; they do not provide direct control or a screenshot of Cornerman's separate game window.

Source evidence:

- `StaffMenuHost.Mount` attaches StaffMenu through `GameManager.ShowUi`.
- `GameManager.ShowUi` adds the component to the shared HUD object.
- Live HUD network inspection reports networking active.
- `StaffMenuHost._instance` is client-local static state, set only by `Toggle`.
- The X handler calls `RequestClose`, which destroys only that registered instance. A menu component delivered in a joining snapshot would render without setting this reference.
- The fallback ScreenPanel object is also created without an explicit non-networked flag.

This strongly supports a join-snapshot/local-lifetime mismatch. Cornerman's local static reference has not been directly inspected. The host later had no menu component or panel and `IsOpen` was false while Cornerman's stuck view was reported.

## Bounded next check

On Cornerman only, run the existing `ulx` console command once and try X again. Source predicts Toggle will detect and replace the unregistered existing component, record the local instance and allow close. This is a diagnostic workaround, not a shipped fix; it sends no moderation or economy command.

Durable candidate direction: put ULX on its own explicitly non-networked, unsaved ScreenPanel before adding the component. Keep source writes out of the active Play session. Re-test joining while the host menu is open, independent opening/closing on both clients, and reconnect. Permission configuration and the Observe rank floor require separate resolution. Existing release ZIP remains unchanged and must not be published as cleared of these new findings.

## Follow-up: Portal/runtime mismatch and scope, 2026-09-14
Read-only Chrome Portal sensor: exact rank 019dff06-68ca-700a-89f2-acdbed20e74a, Super Admin order 9, Inherits None, All Servers, All Permissions (Wildcard), unrestricted access. Host bridge HasRank(Cornerman, same exact ID) returns true, yet inventory.view and player.pocket.view return false. Earlier portal.audit.view and command.waypoint.use also false; command.ban true. This is not evidence that Portal needs broader grants: snapshot freshness, server filtering, and runtime resolution need reconciliation. No Portal changes made.
Host CanTarget(Cornerman, Bloodwave)=false; CanTarget(Cornerman, self)=true. Existing inventory and pocket views require both read grants and rank targetability. Do not remove target protections without a separate explicit ruling; user requested Portal permission truth, not Owner-target escalation.
User contract: staff see Audit, Waypoints, Observe, commands; unavailable operations visible disabled with a clear access reason. Readable navigation does not grant restricted audit, sanctions, inventory, pocket data or ban/unban mutations. Observe arbitrary order-10 floor must go. Remote Audit currently has no transport: bounded requester-only host read, permission revalidation, correlation and revocation-safe cache needed.
Independent read-only reviewer confirmed close registration hole: shared HUD mount, static instance assigned only Toggle, X closes only static instance. Prepare dedicated NetworkMode.Never / NotNetworked / NotSaved root before adding UI, close calling component, preserve pending/unknown money navigation lock and shared HUD. Remote replication remains unverified directly. Workaround typing ulx then X on Cornerman was suggested but no result received.
A single implementer is preparing only two-file lifetime candidate OFF PRODUCT; no live source changes. Play remains live. Latest editor sensor says scene has unsaved changes; never save scene. All current release ZIPs remain unpublished and cannot represent these new fixes.
Account read sensor: regular weekly bucket 97% used (3% remaining), no reset credits. No purchase or reset performed.
## Candidate prepared
Lifetime-only candidate: D:\Cavelux\tools\ulx-qa\release-readiness-20260914\local-menu-candidate-20260914-052319\
Exact diff and CHANGE-REPORT.md include original/candidate SHA256 and CRLF custody. Independent reviewer is checking this candidate. No live-source adoption, compile or remote test yet. The stopped-session boundary is required before applying game source. This candidate does not fix Portal/runtime mismatch or add remote audit transport; do not describe it as the entire release repair.
## Independent candidate review
PASS: static review, no must-fix within lifetime-only scope. Reviewer re-read original and candidate bytes and verified all report hashes and sizes. Money locks, caller-directed close, explicit owned-root teardown, headless and nonnetworked flag ordering, and paired input lifecycle passed. No compile or remote-runtime proof. Await stopped Play before any live-source adoption. No product/Git/Portal changes made.
## Network website Save feedback candidate (reviewed)
User reports persistent network-management success row and wants button-only animated confirmation. Combined candidate r2: D:\Cavelux\tools\ulx-qa\release-readiness-20260914\combined-menu-save-candidate-r2-20260914-053447\ . Three candidate files (Host, Razor, SCSS) include previously reviewed lifetime correction. Saving spinner -> confirmed Saved check for two seconds -> Save; pending/success banner removed, errors retained, edits cancel success, reopen does not replay stale receipt. Local fixture label Previewed chosen in C# Host (Razor preprocessor issue corrected). Independent reviewer PASS, pins verified; compile/runtime/visual unverified. Product unchanged, pending stopped-session adoption. Preserve both package/workbench variant distinctions when later syncing; never overwrite package native adapter guards wholesale.