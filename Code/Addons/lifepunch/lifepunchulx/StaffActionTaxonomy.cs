// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// The ONE home for audit-ring action-name sets (OPUS-ULX-NAMES-1, board 2231).
///
/// Three lanes independently derived overlapping name sets by grep on the same day
/// (OBSERVE-2's staff set, PROFILE-SANCTIONS-1's sanction set, WAYPOINTS-1's teleport set).
/// Divergent copies of one vocabulary rot apart, so they live here and nowhere else. Every
/// consumer reads these; none keeps a local copy.
///
/// TWO RULES THAT LOOK LIKE BUGS AND ARE NOT:
/// 1. These are AUDIT-RING action names, NOT StaffMenuActions catalogue keys. The ring spells
///    the movement acts "Teleport"; the catalogue spells them "goto" / "bring" / "return" /
///    "tpall". GetAuditEntries matches this set exactly and case-insensitively, so passing a
///    catalogue key here matches ZERO rows while still looking plausible.
/// 2. The sets OVERLAP but deliberately DIFFER, because they answer different questions. They
///    are NOT unified and must not be collapsed into one: see each set's own doc.
///
/// NOT HERE (yet): ModerationActionNames and EconomyActionNames still live private in
/// StaffObserveHost. They were never duplicated, so they carry no rot risk, and NAMES-1's
/// scope named only the three sets below. They belong here on the same argument -- flagged
/// in that lane's OPEN QUESTIONS rather than moved unasked.
///
/// EVERY NAME BELOW IS SENSED, NEVER INVENTED: each exists as a first-argument string literal
/// at a ServerApiClient.Audit( call site, or in StaffMenuHost's Portal/Local audit catalogues.
/// </summary>
public static class StaffActionTaxonomy
{
	/// <summary>
	/// Action names only a staff-empowered actor can emit -- the Observe Staff tab's action log
	/// filter (OPUS-ULX-OBSERVE-2). Inclusion rule, sensed at every ServerApiClient.Audit call
	/// site in the tree: a name qualifies when EVERY emitter of that name sits behind a staff
	/// permission gate (a command's RequiredPermissions or an explicit RankSystem.HasPermission
	/// check -- e.g. JobForce behind Permission.CommandJobManage, PocketView behind
	/// Permission.ViewPocket) or inside a staff system (AdminSystem, AdminTicketSystem). Names
	/// with any civilian or RP emitter stay OUT so every row here is attributable to a staff
	/// power: Wanted/Warrant/Lockpick/PoliticalPrisoner/Demote are governance-RP acts,
	/// Waypoint's grant breadth is unproven, and the chat/economy/gameplay names are player
	/// acts. "ModifyBalance" exists only in the portal catalog and the LIFEPUNCH_LOCAL stub rows
	/// (the same caveat carried by the economy set still held privately in StaffObserveHost) and
	/// is included so those rows land where they exist. Overlaps the moderation set in
	/// StaffObserveHost by design: that set answers "what enforcement happened", this one
	/// answers "what did staff DO". Differs from <see cref="SanctionActionNames"/> by design
	/// too -- see that set's note.
	/// TO REGENERATE: re-run the call-site census (recipe on StaffMenuHost.LocalAuditActions)
	/// and re-check each candidate name's gate.
	/// </summary>
	public static readonly string[] StaffActionNames =
	{
		"Warn", "Kick", "Ban", "Gag", "Freeze", "Arrest", "Unarrest", "Status",
		"Spectate", "Fake Disconnect", "Teleport", "SetHealth", "ForceRpName",
		"ForceSellDoor", "ClearEntities", "ClearAllEntities", "ClearAllProps",
		"SpawnItem", "StaffSpawnEntity", "StaffSpawnMarket", "StaffAnnounce",
		"CancelDemote", "JobForce", "PocketView", "StaffTicketClaimed",
		"StaffTicketResolved", "ModifyBalance"
	};

	/// <summary>
	/// Staff or governance acts done TO a player -- the Player Profile's sanction feed
	/// (OPUS-ULX-PROFILE-SANCTIONS-1). SENSED, NEVER INVENTED: every name exists in
	/// StaffMenuHost.PortalAuditActions or StaffMenuHost.LocalAuditActions (the 85-call-site
	/// census). Inclusion rule: the act is done by staff (or a governance power) TO a player.
	/// Excluded by that rule: civilian/RP acts (Lockpick), ticket workflow not done to a player
	/// (StaffTicketClaimed/Resolved), ambiguous names (Status), and all chat/economy/gameplay
	/// names (player acts -- the profile's Recent actions card covers them).
	/// NOTE the ring has NO "Jail" action: the vocabulary spells it Arrest/Unarrest, and this
	/// list follows the ring, not the menu's QuickSanctionKeys labels.
	/// DELIBERATELY NOT <see cref="StaffActionNames"/>: this set adds the governance-RP acts
	/// (Sanction, Demote, VoteDemote, PoliticalPrisoner, Wanted, Warrant) that the staff set
	/// excludes, and drops the not-done-to-a-player names (Status, ForceSellDoor, ClearEntities,
	/// ClearAllEntities, ClearAllProps, SpawnItem, StaffSpawnEntity, StaffSpawnMarket,
	/// StaffAnnounce, StaffTicketClaimed, StaffTicketResolved, ModifyBalance) that it includes.
	/// The two answer different questions. Do not merge them.
	/// TO REGENERATE: grep PortalAuditActions / LocalAuditActions in StaffMenuHost.cs and the
	/// moderation-set census recipe in observe/StaffObserveHost.cs, then re-apply the rule.
	/// </summary>
	public static readonly string[] SanctionActionNames =
	{
		"Warn", "Kick", "Ban", "Gag", "Freeze", "Arrest", "Unarrest", "Sanction",
		"Demote", "CancelDemote", "VoteDemote", "PoliticalPrisoner", "Wanted", "Warrant",
		"Fake Disconnect", "SetHealth", "ForceRpName", "JobForce", "PocketView",
		"Spectate", "Teleport"
	};

	/// <summary>
	/// Player-movement action names -- the Waypoints tab's Recent teleport events feed
	/// (OPUS-ULX-WAYPOINTS-1), sensed at their ServerApiClient.Audit call sites. "Teleport" is
	/// the SINGLE name emitted by BringCommand, GotoCommand, ReturnCommand and TpAllCommand
	/// alike; "Waypoint" is emitted by WaypointCommand's set / clear / go-to paths. Both also
	/// appear in StaffMenuHost.LocalAuditActions.
	/// DELIBERATELY BROADER THAN <see cref="StaffActionNames"/> ON ONE NAME: the staff set
	/// excludes "Waypoint" because its grant breadth is unproven, while this feed is about
	/// movement rather than staff attribution and so keeps it. Do not reconcile them by
	/// deleting one.
	/// TO REGENERATE: grep the tree for ServerApiClient.Audit( and take the first string literal
	/// of each call.
	/// </summary>
	public static readonly string[] TeleportActionNames = { "Teleport", "Waypoint" };
}
