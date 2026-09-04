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

using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// One sentinel/anticheat violation event (reserved). Shape mirrors the dormant
/// SentinelDto (game/Code/Api/Dtos/SentinelDto.cs) plus a display timestamp -- the fields
/// Sentinel.RecordViolation already has in hand when it fires.
/// </summary>
public readonly record struct ObserveSentinelEventVm(
	string When,
	long SteamId,
	string SteamName,
	string Exploit,
	string Detail );

/// <summary>One balance-delta event (reserved). All strings pre-formatted on the data side
/// so the view cannot invent a precision or a currency sign; IsCredit drives tone only.</summary>
public readonly record struct ObserveBalanceDeltaVm(
	string When,
	long SteamId,
	string Name,
	string Amount,
	bool IsCredit,
	string Source );

/// <summary>One offline staff member with portal-side activity (reserved).</summary>
public readonly record struct ObserveOfflineStaffVm(
	long SteamId,
	string Name,
	string Role,
	string RankColorHex,
	string LastSeen,
	string TotalPlaytime );

/// <summary>
/// Reserved data sockets for the Observe views whose data source DOES NOT EXIST yet --
/// the Banker-socket style (serverhub LpServerHubBankerSocket precedent): a display-only
/// VM contract with zero logic, null meaning "not wired", so the view renders an honest
/// empty state instead of fixture rows (IsLive semantics per the serverhub precedent --
/// "no source is wired" must never blur into "nothing happened"). A future supplying lane
/// sets the properties; the field shapes are a starting proposal, theirs to reshape.
/// DELIBERATELY ABSENT: any mutation, RPC, command, or computation.
///
/// Why each socket is empty today (sensed, not assumed):
///  -- Sentinel: Sentinel.RecordViolation (game/Code/Sentinel/Sentinel.cs) logs, POSTs a
///     portal sanction and forces a screenshot, but stores nothing locally and syncs
///     nothing; SentinelDto has zero call sites. There is no violation feed to read.
///  -- EconomyDeltas: no transaction/delta history exists anywhere in game code; current
///     balances are synced but per-event history is portal-side only, and the remote audit
///     GET is unbound (TECH_DEBT STAFF-07). Bank-branch movements (salary, staff bank
///     grants, BTC cash-outs) never touch the local ring at all.
///  -- OfflineStaff: the portal's full rank-assignment map is synced to clients but held in
///     private NetDictionaries with no bulk accessor, and no last-seen / offline-playtime
///     source exists in this tree.
/// </summary>
internal static class StaffObserveSockets
{
	/// <summary>Sentinel violation feed. Null until a supplying lane wires
	/// Sentinel.RecordViolation (or the portal sanction trail) to a readable feed.</summary>
	public static IReadOnlyList<ObserveSentinelEventVm>? SentinelEvents { get; set; }

	/// <summary>Balance-delta feed for spike review. Null until a transaction history
	/// exists (PayHost/ChargeHost seam capture, or the portal audit GET being bound).</summary>
	public static IReadOnlyList<ObserveBalanceDeltaVm>? EconomyDeltas { get; set; }

	/// <summary>Offline staff roster with activity. Null until a rank-assignment
	/// enumerator or a portal roster read exists.</summary>
	public static IReadOnlyList<ObserveOfflineStaffVm>? OfflineStaff { get; set; }
}
