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

using System;
using System.Collections.Generic;
using System.Linq;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// One online player carrying at least one active state flag right now (Observe "Live flag
/// scan" row). Flags come straight from <see cref="StaffMenuHost.GetStateFlags"/> -- a
/// point-in-time read, not a history.
/// </summary>
public readonly record struct ObserveFlaggedRow(
	StaffMenuPlayer Player,
	IReadOnlyList<StaffStateFlag> Flags,
	bool AnyIllegitimate );

/// <summary>
/// One online player's live synced balances (Observe "Largest holdings" row). These are the
/// [Sync(FromHost)] WalletBalance/BankBalance values every client already receives -- current
/// truth, not history.
/// </summary>
public readonly record struct ObserveHoldingRow(
	long SteamId,
	string Name,
	string Role,
	string RankColorHex,
	uint Wallet,
	uint Bank,
	ulong Total );

/// <summary>
/// One online staff member with an activity summary (Observe "Online staff" row).
/// RingEventCount counts this actor's rows in the host audit ring -- newest 500, current
/// boot, host process only -- so the view labels it as ring events, never as history.
/// </summary>
public readonly record struct ObserveStaffRow(
	StaffMenuPlayer Player,
	int RingEventCount,
	IReadOnlyList<StaffStateFlag> Flags );

/// <summary>
/// Read model for the Observe section -- the Superadmin+ forensic surface mounted at the
/// Player and Staff Management view (OPUS-ULX-OBSERVE-1). READ-ONLY BY DESIGN: v1 watches
/// and reports, it acts on nothing (actions stay in Moderation / Commands). Every accessor
/// here reads an EXISTING source through <see cref="StaffMenuHost"/>; this type deliberately
/// touches nothing but StaffMenuHost, Sandbox engine statics and the BCL, so it parses and
/// behaves identically in both build arms (LIFEPUNCH_LOCAL editor stub vs live DXRP) without
/// preprocessor splits of its own. Where a view has NO existing source, the honest
/// empty-state + reserved socket lives in <see cref="StaffObserveSockets"/> instead --
/// nothing here invents telemetry or fabricates rows.
/// </summary>
internal static class StaffObserveHost
{
	// ── Superadmin+ gate ──────────────────────────────────────────────────────────────

	/// <summary>
	/// The Superadmin-plus floor on the live rank ladder (Mod=4, Admin=5, Super Admin=10,
	/// Owner=69). Same sensed constant the house already pins in
	/// <see cref="StaffMenuConfig.MaxBanHoursForRankOrder"/>, where order >= 10 is the
	/// Super Admin / Owner tier. No tier enum exists in DXRP -- tier is portal rank Order.
	/// </summary>
	public const int SuperadminPlusMinRankOrder = 10;

	/// <summary>
	/// Gate for the whole Observe section, composed strictly from EXISTING machinery: the
	/// Permission.ViewAudit id ("portal.audit.view") via
	/// <see cref="StaffMenuHost.AuditPermissionId"/> -- the standing forensic-view
	/// permission, the same gate the Audit tab wears; consumed as the string const because
	/// the Permission enum type is unavailable in the LIFEPUNCH_LOCAL arm (TECH_DEBT
	/// STAFF-01 precedent) -- AND the Super Admin rank-order floor above. UX gating only,
	/// like every CanView in this addon; Observe is read-only and dispatches nothing, so
	/// there is no host action behind it to re-check. In the LIFEPUNCH_LOCAL arm CanView
	/// returns true and LocalRankOrder is stubbed to 10, so the section renders in the
	/// editor without a live portal.
	/// </summary>
	public static bool CanViewObserve() =>
		StaffMenuHost.CanView( StaffMenuHost.AuditPermissionId )
		&& StaffMenuHost.LocalRankOrder >= SuperadminPlusMinRankOrder;

	// ── Audit-ring action sets (existing names only) ─────────────────────────────────

	/// <summary>
	/// Enforcement / moderation action names that actually land in the host audit ring
	/// today, sensed at their ServerApiClient.Audit call sites -- exact existing strings,
	/// none invented. GetAuditEntries treats the set as exact case-insensitive matches.
	/// </summary>
	private static readonly string[] ModerationActionNames =
	{
		"Warn", "Kick", "Ban", "Gag", "Freeze", "Arrest", "Unarrest", "Status",
		"Wanted", "Warrant", "Spectate", "Fake Disconnect", "Demote",
		"PoliticalPrisoner", "StaffTicketClaimed", "StaffTicketResolved"
	};

	/// <summary>
	/// Money-movement action names that land in the ring today (the wallet-side rail plus
	/// gameplay pay-outs; sensed call sites). Bank-branch movements -- salary, staff bank
	/// grants, BTC cash-outs -- never reach the ring at all; see the EconomyDeltas socket.
	/// "ModifyBalance" exists only in the portal catalog and the LIFEPUNCH_LOCAL stub rows,
	/// and is included so those rows filter correctly where they exist.
	/// </summary>
	private static readonly string[] EconomyActionNames =
	{
		"WalletCharge", "WalletDeposit", "ATM", "MoneySpawn", "Recycler", "CoinFlip",
		"SlotMachineCashOut", "VoteBet", "Hit", "MayorTown", "LifePunchBtcPayout",
		"MysteryBoxWin", "ModifyBalance"
	};

	/// <summary>
	/// The audit ring lives in the host process only (its remote GET is unbound --
	/// TECH_DEBT STAFF-07), so ring-backed views say WHERE they are empty instead of
	/// letting an empty client ring read as "nothing happened".
	/// </summary>
	public static bool RingLivesHere => StaffMenuHost.AuditFeedIsReadableHere;

	/// <summary>Shared truthful reason ring-backed Observe surfaces are unavailable here.</summary>
	public static string RingUnavailableText => StaffMenuHost.AuditFeedUnavailableText;

	// ── View reads (existing sources only) ───────────────────────────────────────────

	/// <summary>Newest-first enforcement events from the host audit ring.</summary>
	public static IReadOnlyList<StaffAuditEntry> ModerationEvents() =>
		StaffMenuHost.GetAuditEntries( "", "", false, ModerationActionNames );

	/// <summary>Newest-first money events from the host audit ring. Amounts live inside the
	/// free-text descriptions (the ring has no numeric field); callers must render them
	/// verbatim and compute nothing from them.</summary>
	public static IReadOnlyList<StaffAuditEntry> EconomyEvents() =>
		StaffMenuHost.GetAuditEntries( "", "", false, EconomyActionNames );

	/// <summary>
	/// Newest-first staff actions from the host audit ring -- the Staff tab's action log.
	/// The same rail the Audit tab reads, filtered to <see cref="StaffActionTaxonomy.StaffActionNames"/>; a
	/// non-zero <paramref name="actorSteamId"/> keeps only that actor's rows (field-exact
	/// actor match, the Audit tab's Player ID semantics). The target of an act is never a
	/// ring field (see AuditPlayerScope) -- it survives only inside Description, which
	/// callers render verbatim.
	/// </summary>
	public static IReadOnlyList<StaffAuditEntry> StaffActionLog( long actorSteamId = 0 ) =>
		StaffMenuHost.GetAuditEntries(
			actorSteamId == 0 ? "" : actorSteamId.ToString(), "", false, StaffActionTaxonomy.StaffActionNames );

	/// <summary>
	/// Online players carrying at least one active state flag right now, illegitimate
	/// carriers first (an active power WITHOUT the granting permission is the loudest thing
	/// on this feed). Point-in-time scan over existing accessors; nothing is recorded.
	/// </summary>
	public static IReadOnlyList<ObserveFlaggedRow> FlaggedOnlinePlayers()
	{
		var rows = new List<ObserveFlaggedRow>();
		foreach ( var player in StaffMenuHost.OnlinePlayers() )
		{
			var flags = StaffMenuHost.GetStateFlags( player.SteamId );
			if ( flags.Count == 0 )
			{
				continue;
			}

			rows.Add( new ObserveFlaggedRow( player, flags, flags.Any( f => f.Illegitimate ) ) );
		}

		return rows
			.OrderByDescending( r => r.AnyIllegitimate )
			.ThenBy( r => r.Player.Name )
			.ToList();
	}

	/// <summary>
	/// Online players ordered by current total holdings (wallet + bank), largest first,
	/// capped at <paramref name="max"/>. Live synced balances -- current truth for a human
	/// eye, NOT spike detection; deltas need a history source that does not exist yet
	/// (see <see cref="StaffObserveSockets.EconomyDeltas"/>).
	/// </summary>
	public static IReadOnlyList<ObserveHoldingRow> TopHoldings( int max )
	{
		var rows = new List<ObserveHoldingRow>();
		foreach ( var player in StaffMenuHost.OnlinePlayers() )
		{
			if ( StaffMenuHost.IsSyntheticPlayer( player.SteamId ) )
			{
				continue;
			}

			var detail = StaffMenuHost.GetPlayerDetail( player.SteamId );
			if ( !detail.Found )
			{
				continue;
			}

			rows.Add( new ObserveHoldingRow(
				player.SteamId, player.Name, player.Role, player.RankColorHex,
				detail.Wallet, detail.Bank, (ulong)detail.Wallet + detail.Bank ) );
		}

		return rows
			.OrderByDescending( r => r.Total )
			.ThenBy( r => r.Name )
			.Take( max )
			.ToList();
	}

	/// <summary>
	/// Online staff with an activity summary: portal rank grouping (highest first), live
	/// playtime, this boot's ring event count per actor, and their current state flags.
	/// Offline staff have no existing enumerable source (the synced rank-assignment map has
	/// no bulk accessor) -- see <see cref="StaffObserveSockets.OfflineStaff"/>.
	/// </summary>
	public static IReadOnlyList<ObserveStaffRow> StaffActivity()
	{
		var counts = new Dictionary<long, int>();
		foreach ( var entry in StaffMenuHost.GetAuditEntries( "", "" ) )
		{
			if ( entry.PlayerSteamId == 0 )
			{
				continue;
			}

			counts[entry.PlayerSteamId] = counts.TryGetValue( entry.PlayerSteamId, out var n ) ? n + 1 : 1;
		}

		var rows = new List<ObserveStaffRow>();
		foreach ( var player in StaffMenuHost.OnlinePlayers() )
		{
			if ( player.GroupName == StaffMenuHost.NonStaffGroup )
			{
				continue;
			}

			rows.Add( new ObserveStaffRow(
				player,
				counts.TryGetValue( player.SteamId, out var n ) ? n : 0,
				StaffMenuHost.GetStateFlags( player.SteamId ) ) );
		}

		return rows
			.OrderByDescending( r => r.Player.GroupOrder )
			.ThenBy( r => r.Player.Name )
			.ToList();
	}
}
