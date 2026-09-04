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
/// Why a parity field shows what it shows. This is the whole point of the parity layer: a portal page
/// that is wrong is worse than a portal page that is absent, so every value carries the reason it is
/// trustworthy — or the reason it is not — and the UI is expected to render the two differently.
/// </summary>
/// <remarks>
/// The distinction that forced this enum is real and specific. <c>GameNetworkManager.ServerName</c> and
/// <c>MaxPlayers</c> are plain <c>static</c> fields with NO <c>[Sync]</c>, assigned only inside the
/// host-only <c>ServerApiLink.Initialize()</c>. On a client they therefore hold their compile-time
/// defaults — "Unknown Server" and 128 — which look exactly like real answers. A panel that printed them
/// on a client would be fabricating server identity with total confidence. <see cref="HostOnly"/> exists
/// so that value never reaches a viewer who cannot actually see it.
/// </remarks>
public enum LpParityAvailability
{
	/// <summary>Read from a host-authoritative or host-synced source that this viewer genuinely sees.</summary>
	Live,

	/// <summary>The source exists but only the host can read it, and this viewer is not the host.</summary>
	HostOnly,

	/// <summary>The server carries no portal authorization key, so no portal-fed value exists to show.</summary>
	Unlinked,

	/// <summary>Linked, but the portal has not answered yet (config not marked ready).</summary>
	Pending,

	/// <summary>Catalogued as unreachable from the game process at all — portal browser only.</summary>
	PortalOnly
}

/// <summary>
/// One labelled value in a parity view, carrying its own provenance. Define-free (no Dxura types) so it
/// flows through a Dxura-free razor exactly as <see cref="StaffMenuPlayer"/> and friends already do.
/// </summary>
/// <remarks>
/// <see cref="Value"/> is display-ready — the host owns formatting so the razor stays logic-light, the
/// same division of labour <c>StaffAuditEntry.When</c> already uses. When <see cref="Availability"/> is
/// anything other than <see cref="LpParityAvailability.Live"/>, <see cref="Value"/> is a short honest
/// empty-state ("host only", "not linked"), never a plausible-looking placeholder.
/// </remarks>
public readonly record struct LpParityField(
	string Label,
	string Value,
	LpParityAvailability Availability );

/// <summary>A titled group of <see cref="LpParityField"/> rows — one card in a parity view.</summary>
public readonly record struct LpParitySection(
	string Title,
	IReadOnlyList<LpParityField> Fields );

/// <summary>Where a portal capability can live.</summary>
public enum LpParityClass
{
	/// <summary>The data is already inside the game process; a read surface needs no new transport.</summary>
	InGameRead,

	/// <summary>A mutating path already exists in-tree that in-game UI could drive.</summary>
	InGameAction,

	/// <summary>No seam exists in this tree; reaching it means new backend endpoints.</summary>
	PortalOnly
}

/// <summary>
/// One row of the parity catalogue: a portal capability, where it can live, and the sensor that decided
/// it. Shipped in-game so an owner can see the parity map itself rather than being told about it.
/// </summary>
/// <remarks>
/// <see cref="Sensor"/> is the file or symbol the classification was read from. It is carried into the
/// build deliberately: a classification without its sensor is an opinion, and this table is meant to be
/// re-checkable by whoever reads it next, not taken on trust.
/// </remarks>
public readonly record struct LpParityCatalogRow(
	string Scope,
	string Capability,
	LpParityClass Class,
	bool Shipped,
	bool PrincipalDomain,
	string Sensor );

/// <summary>
/// One rank observed on this server, projected free of Dxura types. Mirrors the portal Ranks page columns
/// for the subset the game can actually see.
/// </summary>
/// <remarks>
/// PARTIAL BY CONSTRUCTION, and the UI must say so. <c>RankSystem</c>'s rank dictionary is <c>private</c>,
/// so there is no way to enumerate the tenant's full rank roster from an addon. What IS reachable is
/// <c>GetPlayerRank(steamId)</c>, which returns a complete <c>RankDto</c> — permission list included — for
/// any player. So this list is exactly "the ranks currently held by someone online", which is a true
/// statement about a real subset, not a truncated attempt at the whole.
/// </remarks>
public readonly record struct LpParityRank(
	string Name,
	int Order,
	string ColorHex,
	bool IsDefault,
	int PermissionCount,
	int HoldersOnline,
	bool HasWildcard );
